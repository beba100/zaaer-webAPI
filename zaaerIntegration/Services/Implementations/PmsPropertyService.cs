using System.Text.Json;
using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms.Property;
using zaaerIntegration.Services;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    public sealed class PmsPropertyService : IPmsPropertyService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private static readonly string[] DefaultServiceOptions =
        {
            "wifi", "ac", "tv", "minibar", "safe", "balcony", "parking"
        };

        private readonly ApplicationDbContext _context;
        private readonly ITenantService _tenantService;
        private readonly INumberingService _numberingService;

        public PmsPropertyService(
            ApplicationDbContext context,
            ITenantService tenantService,
            INumberingService numberingService)
        {
            _context = context;
            _tenantService = tenantService;
            _numberingService = numberingService;
        }

        public async Task<int> ResolveCurrentHotelIdAsync(CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            return scope.ScopeHotelId;
        }

        public async Task<PmsPropertyModeDto> GetPropertyModeAsync(CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var mode = PropertyTypes.ResolveMode(scope.PropertyType);
            return new PmsPropertyModeDto
            {
                PropertyType = mode,
                IsResort = PropertyTypes.IsResort(mode),
                IsHall = PropertyTypes.IsHall(mode),
                IsHotel = PropertyTypes.IsHotel(mode)
            };
        }

        public async Task<PmsPropertyLookupsDto> GetLookupsAsync(CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var buildings = await ListBuildingsAsync(cancellationToken);
            var buildingsForLookup = await _context.Buildings.AsNoTracking()
                .Where(b => (b.HotelId == scope.ScopeHotelId || b.HotelId == scope.LocalHotelId))
                .ToListAsync(cancellationToken);

            var floorRows = await _context.Floors.AsNoTracking()
                .Where(f => (f.HotelId == scope.ScopeHotelId || f.HotelId == scope.LocalHotelId))
                .OrderBy(f => f.SortOrder)
                .ThenBy(f => f.FloorNumber)
                .ToListAsync(cancellationToken);

            var floors = floorRows.Select(f =>
            {
                var parent = buildingsForLookup.FirstOrDefault(b =>
                    PropertyEntityLinks.FloorBelongsToBuilding(f, b));
                return new PmsPropertyFloorLineDto
                {
                    FloorId = f.FloorId,
                    ZaaerId = f.ZaaerId,
                    BuildingZaaerId = parent?.ZaaerId ?? f.BuildingId,
                    FloorNumber = f.FloorNumber,
                    FloorName = f.FloorName,
                    SortOrder = f.SortOrder,
                    IsActive = f.IsActive
                };
            }).ToList();

            var roomTypes = await ListRoomTypesAsync(cancellationToken);

            var facilityRows = await _context.Facilities.AsNoTracking()
                .Where(f => (f.HotelId == scope.ScopeHotelId || f.HotelId == scope.LocalHotelId) && f.IsActive)
                .OrderBy(f => f.FacilityName)
                .ToListAsync(cancellationToken);

            var facilityOptions = facilityRows.Select(f => new PmsPropertyFacilityOptionDto
            {
                FacilityId = f.FacilityId,
                ZaaerId = f.ZaaerId,
                FacilityName = f.FacilityName,
                FacilityNameEn = f.FacilityNameEn,
                Description = f.Description,
                IsActive = f.IsActive
            }).ToList();

            var mode = PropertyTypes.ResolveMode(scope.PropertyType);
            var labels = BuildPropertyModeLabels(mode);

            return new PmsPropertyLookupsDto
            {
                Buildings = buildings.ToList(),
                Floors = floors,
                RoomTypes = roomTypes.ToList(),
                KitchenTypes = PropertyKitchenTypes.All.ToList(),
                HallTypes = PropertyHallTypes.All.ToList(),
                PropertyTypes = PropertyTypes.All.ToList(),
                PropertyType = mode,
                IsResort = PropertyTypes.IsResort(mode),
                IsHall = PropertyTypes.IsHall(mode),
                IsHotel = PropertyTypes.IsHotel(mode),
                Labels = labels,
                HallEventTypes = HallEventTypes.All.ToList(),
                HallEventStatuses = FinanceLedgerAPI.Enums.HallEventStatusCodes.ToLookupList()
                    .Select(x => x.Value)
                    .ToList(),
                HallGenderTypes = HallGenderTypes.All.ToList(),
                HallVenueKinds = HallVenueKinds.All.ToList(),
                HallPreparationStatuses = HallPreparationStatuses.All.ToList(),
                PackagePriceTypes = PackagePriceTypes.All.ToList(),
                PackageCategories = PackageCategories.All.ToList(),
                RoomCategories = PropertyRoomCategories.All.ToList(),
                ResortAreaTypes = ResortAreaTypes.All.ToList(),
                ServiceOptions = DefaultServiceOptions.ToList(),
                Facilities = facilityOptions
            };
        }

        public async Task<IReadOnlyList<PmsBuildingListItemDto>> ListBuildingsAsync(CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var buildings = await _context.Buildings.AsNoTracking()
                .Where(b => (b.HotelId == scope.ScopeHotelId || b.HotelId == scope.LocalHotelId))
                .OrderBy(b => b.BuildingName)
                .ToListAsync(cancellationToken);

            var floors = await _context.Floors.AsNoTracking()
                .Where(f => (f.HotelId == scope.ScopeHotelId || f.HotelId == scope.LocalHotelId))
                .ToListAsync(cancellationToken);

            var apartmentLinks = await QueryApartmentScopeRowsAsync(scope, cancellationToken);

            return buildings.Select(b =>
            {
                return new PmsBuildingListItemDto
                {
                    BuildingId = b.BuildingId,
                    ZaaerId = b.ZaaerId,
                    BuildingName = b.BuildingName ?? string.Empty,
                    Description = b.Description ?? b.Address,
                    IsActive = b.IsActive,
                    FloorCount = PropertyEntityLinks.FloorsForBuilding(floors, b).Count(),
                    ApartmentCount = apartmentLinks.Count(a => PropertyEntityLinks.ApartmentReferencesBuilding(a.BuildingId, b))
                };
            }).ToList();
        }

        public async Task<PmsBuildingDetailDto?> GetBuildingAsync(int id, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var building = await ResolveBuildingAsync(id, scope, cancellationToken);
            if (building == null)
            {
                return null;
            }

            return await MapBuildingDetailAsync(building, cancellationToken);
        }

        public async Task<PmsBuildingDetailDto> CreateBuildingAsync(PmsUpsertBuildingDto dto, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var auditIds = new List<long>();

            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var buildingZaaer = await _numberingService.GetNextEntityZaaerIdAsync(
                    NumberingDocCodes.Building,
                    "pms-property-building",
                    $"pms-building:{scope.ScopeHotelId}:{Guid.NewGuid():N}",
                    cancellationToken);
                auditIds.Add(buildingZaaer.AuditId);

                var building = new Building
                {
                    HotelId = scope.ScopeHotelId,
                    BuildingName = dto.BuildingName.Trim(),
                    BuildingNumber = string.Empty,
                    Address = dto.Description?.Trim() ?? string.Empty,
                    Description = dto.Description?.Trim(),
                    IsActive = dto.IsActive,
                    ZaaerId = ZaaerIdMapper.ToNullableInt32(buildingZaaer.ZaaerId)
                };

                _context.Buildings.Add(building);
                await _context.SaveChangesAsync(cancellationToken);

                var buildingLinkId = PropertyEntityLinks.GetBuildingLinkId(building);
                await SyncBuildingFloorsAsync(building, scope, buildingLinkId, dto.Floors, auditIds, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                foreach (var auditId in auditIds)
                {
                    await _numberingService.MarkCommittedAsync(auditId, cancellationToken);
                }

                await tx.CommitAsync(cancellationToken);
                return (await MapBuildingDetailAsync(building, cancellationToken))!;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken);
                foreach (var auditId in auditIds)
                {
                    await _numberingService.MarkVoidedAsync(auditId, ex.Message, cancellationToken);
                }

                throw;
            }
        }

        public async Task<PmsBuildingDetailDto?> UpdateBuildingAsync(int id, PmsUpsertBuildingDto dto, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var building = await ResolveBuildingAsync(id, scope, cancellationToken, tracking: true);
            if (building == null)
            {
                return null;
            }

            if (!building.ZaaerId.HasValue)
            {
                var z = await _numberingService.GetNextEntityZaaerIdAsync(
                    NumberingDocCodes.Building,
                    "pms-property-building-backfill",
                    $"pms-building-backfill:{building.BuildingId}",
                    cancellationToken);
                building.ZaaerId = ZaaerIdMapper.ToNullableInt32(z.ZaaerId);
                await _numberingService.MarkCommittedAsync(z.AuditId, cancellationToken);
            }

            var auditIds = new List<long>();
            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                building.BuildingName = dto.BuildingName.Trim();
                building.Description = dto.Description?.Trim();
                building.Address = dto.Description?.Trim() ?? building.Address ?? string.Empty;
                building.IsActive = dto.IsActive;

                var buildingLinkId = PropertyEntityLinks.GetBuildingLinkId(building);
                await SyncBuildingFloorsAsync(building, scope, buildingLinkId, dto.Floors, auditIds, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                foreach (var auditId in auditIds)
                {
                    await _numberingService.MarkCommittedAsync(auditId, cancellationToken);
                }

                await tx.CommitAsync(cancellationToken);
                return await MapBuildingDetailAsync(building, cancellationToken);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken);
                foreach (var auditId in auditIds)
                {
                    await _numberingService.MarkVoidedAsync(auditId, ex.Message, cancellationToken);
                }

                throw;
            }
        }

        public async Task<bool> DeleteBuildingAsync(int id, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var building = await ResolveBuildingAsync(id, scope, cancellationToken, tracking: true);
            if (building == null)
            {
                return false;
            }

            var hasUnits = await _context.Apartments.AsNoTracking()
                .Where(a => (a.HotelId == scope.ScopeHotelId || a.HotelId == scope.LocalHotelId))
                .AnyAsync(a =>
                    a.BuildingId == building.BuildingId ||
                    (building.ZaaerId.HasValue && a.BuildingId == building.ZaaerId),
                    cancellationToken);

            if (hasUnits)
            {
                throw new InvalidOperationException("Cannot delete block while units are assigned.");
            }

            var floors = await _context.Floors
                .Where(f => (f.HotelId == scope.ScopeHotelId || f.HotelId == scope.LocalHotelId))
                .ToListAsync(cancellationToken);

            foreach (var floor in PropertyEntityLinks.FloorsForBuilding(floors, building))
            {
                _context.Floors.Remove(floor);
            }

            _context.Buildings.Remove(building);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<IReadOnlyList<PmsRoomTypeListItemDto>> ListRoomTypesAsync(CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var roomTypes = await _context.RoomTypes.AsNoTracking()
                .Where(rt => (rt.HotelId == scope.ScopeHotelId || rt.HotelId == scope.LocalHotelId))
                .OrderBy(rt => rt.SortOrder)
                .ThenBy(rt => rt.RoomTypeName)
                .ToListAsync(cancellationToken);

            var apartmentLinks = await QueryApartmentScopeRowsAsync(scope, cancellationToken);

            return roomTypes.Select(rt => MapRoomType(rt, apartmentLinks)).ToList();
        }

        public async Task<PmsRoomTypeListItemDto?> GetRoomTypeAsync(int id, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var rt = await ResolveRoomTypeAsync(id, scope, cancellationToken);
            if (rt == null)
            {
                return null;
            }

            var apartmentLinks = await QueryApartmentScopeRowsAsync(scope, cancellationToken);

            return MapRoomType(rt, apartmentLinks);
        }

        public async Task<PmsRoomTypeListItemDto> CreateRoomTypeAsync(PmsUpsertRoomTypeDto dto, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var auditIds = new List<long>();

            try
            {
                var identity = await _numberingService.GetNextEntityZaaerIdAsync(
                    NumberingDocCodes.RoomType,
                    "pms-property-room-type",
                    $"pms-room-type:{scope.ScopeHotelId}:{Guid.NewGuid():N}",
                    cancellationToken);
                auditIds.Add(identity.AuditId);

                var entity = new RoomType
                {
                    HotelId = scope.ScopeHotelId,
                    RoomTypeName = dto.RoomTypeName.Trim(),
                    RoomTypeNameEn = dto.RoomTypeNameEn?.Trim(),
                    RoomTypeDesc = dto.RoomTypeDesc?.Trim() ?? string.Empty,
                    RoomCategory = NormalizeRoomCategory(dto.RoomCategory),
                    RoomCount = dto.RoomCount,
                    SortOrder = dto.SortOrder,
                    IsActive = dto.IsActive,
                    ImageUrl = dto.ImageUrl?.Trim(),
                    ZaaerId = ZaaerIdMapper.ToNullableInt32(identity.ZaaerId)
                };

                _context.RoomTypes.Add(entity);
                await _context.SaveChangesAsync(cancellationToken);

                foreach (var auditId in auditIds)
                {
                    await _numberingService.MarkCommittedAsync(auditId, cancellationToken);
                }

                return MapRoomType(entity, Array.Empty<ApartmentScopeRow>());
            }
            catch (Exception ex)
            {
                foreach (var auditId in auditIds)
                {
                    await _numberingService.MarkVoidedAsync(auditId, ex.Message, cancellationToken);
                }

                throw;
            }
        }

        public async Task<PmsRoomTypeListItemDto?> UpdateRoomTypeAsync(int id, PmsUpsertRoomTypeDto dto, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var entity = await ResolveRoomTypeAsync(id, scope, cancellationToken, tracking: true);
            if (entity == null)
            {
                return null;
            }

            if (!entity.ZaaerId.HasValue)
            {
                var z = await _numberingService.GetNextEntityZaaerIdAsync(
                    NumberingDocCodes.RoomType,
                    "pms-property-room-type-backfill",
                    $"pms-room-type-backfill:{entity.RoomTypeId}",
                    cancellationToken);
                entity.ZaaerId = ZaaerIdMapper.ToNullableInt32(z.ZaaerId);
                await _numberingService.MarkCommittedAsync(z.AuditId, cancellationToken);
            }

            entity.RoomTypeName = dto.RoomTypeName.Trim();
            entity.RoomTypeNameEn = dto.RoomTypeNameEn?.Trim();
            entity.RoomTypeDesc = dto.RoomTypeDesc?.Trim() ?? string.Empty;
            entity.RoomCategory = NormalizeRoomCategory(dto.RoomCategory);
            entity.RoomCount = dto.RoomCount;
            entity.SortOrder = dto.SortOrder;
            entity.IsActive = dto.IsActive;
            entity.ImageUrl = dto.ImageUrl?.Trim();

            await _context.SaveChangesAsync(cancellationToken);

            var apartmentLinks = await QueryApartmentScopeRowsAsync(scope, cancellationToken);

            return MapRoomType(entity, apartmentLinks);
        }

        public async Task<bool> DeleteRoomTypeAsync(int id, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var entity = await ResolveRoomTypeAsync(id, scope, cancellationToken, tracking: true);
            if (entity == null)
            {
                return false;
            }

            var linkId = PropertyEntityLinks.GetRoomTypeLinkId(entity);
            var inUse = await _context.Apartments.AnyAsync(
                a => (a.HotelId == scope.ScopeHotelId || a.HotelId == scope.LocalHotelId) && a.RoomTypeId != null &&
                     (a.RoomTypeId == entity.RoomTypeId || a.RoomTypeId == linkId),
                cancellationToken);

            if (inUse)
            {
                throw new InvalidOperationException("Cannot delete room type while units are assigned.");
            }

            _context.RoomTypes.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<IReadOnlyList<PmsApartmentListItemDto>> ListApartmentsAsync(
            string? search = null,
            int? buildingZaaerId = null,
            int? floorZaaerId = null,
            int? roomTypeZaaerId = null,
            int? parentApartmentZaaerId = null,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var query = _context.Apartments.AsNoTracking().Where(a => (a.HotelId == scope.ScopeHotelId || a.HotelId == scope.LocalHotelId));

            if (parentApartmentZaaerId.HasValue)
            {
                var parent = await _context.Apartments.AsNoTracking()
                    .Where(a => (a.HotelId == scope.ScopeHotelId || a.HotelId == scope.LocalHotelId)
                        && (a.ApartmentId == parentApartmentZaaerId.Value || a.ZaaerId == parentApartmentZaaerId.Value))
                    .Select(a => new { a.ApartmentId, a.ZaaerId })
                    .FirstOrDefaultAsync(cancellationToken);

                if (parent == null)
                {
                    return Array.Empty<PmsApartmentListItemDto>();
                }

                var parentLinkId = parent.ZaaerId ?? parent.ApartmentId;
                query = query.Where(a =>
                    a.ParentApartmentId == parentLinkId ||
                    a.ParentApartmentId == parent.ApartmentId ||
                    (parent.ZaaerId.HasValue && a.ParentApartmentId == parent.ZaaerId));
            }
            else if (PropertyTypes.IsResort(scope.PropertyType))
            {
                query = query.Where(a => !a.ParentApartmentId.HasValue);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(a =>
                    a.ApartmentCode.Contains(term) ||
                    (a.ApartmentName != null && a.ApartmentName.Contains(term)));
            }

            if (buildingZaaerId.HasValue)
            {
                query = query.Where(a => a.BuildingId == buildingZaaerId.Value);
            }

            if (floorZaaerId.HasValue)
            {
                query = query.Where(a => a.FloorId == floorZaaerId.Value);
            }

            if (roomTypeZaaerId.HasValue)
            {
                query = query.Where(a => a.RoomTypeId == roomTypeZaaerId.Value);
            }

            var rows = await ProjectApartmentListRow(query.OrderBy(a => a.ApartmentCode))
                .ToListAsync(cancellationToken);
            return await MapApartmentsAsync(scope, rows, cancellationToken);
        }

        public async Task<PmsApartmentListItemDto?> GetApartmentAsync(int id, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var apartment = await ResolveApartmentAsync(id, scope, cancellationToken);
            if (apartment == null)
            {
                return null;
            }

            var list = await MapApartmentsAsync(scope, new[] { apartment }, cancellationToken);
            return list.FirstOrDefault();
        }

        public async Task<PmsApartmentListItemDto> CreateApartmentAsync(PmsUpsertApartmentDto dto, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var auditIds = new List<long>();

            try
            {
                var identity = await _numberingService.GetNextEntityZaaerIdAsync(
                    NumberingDocCodes.Apartment,
                    "pms-property-apartment",
                    $"pms-apartment:{scope.ScopeHotelId}:{Guid.NewGuid():N}",
                    cancellationToken);
                auditIds.Add(identity.AuditId);

                var entity = await MapApartmentEntityAsync(dto, scope, cancellationToken);
                entity.ZaaerId = ZaaerIdMapper.ToNullableInt32(identity.ZaaerId);

                _context.Apartments.Add(entity);
                await _context.SaveChangesAsync(cancellationToken);

                foreach (var auditId in auditIds)
                {
                    await _numberingService.MarkCommittedAsync(auditId, cancellationToken);
                }

                var list = await MapApartmentsAsync(scope, new[] { entity }, cancellationToken);
                return list[0];
            }
            catch (Exception ex)
            {
                foreach (var auditId in auditIds)
                {
                    await _numberingService.MarkVoidedAsync(auditId, ex.Message, cancellationToken);
                }

                throw;
            }
        }

        public async Task<PmsApartmentListItemDto?> UpdateApartmentAsync(int id, PmsUpsertApartmentDto dto, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var entity = await ResolveApartmentAsync(id, scope, cancellationToken, tracking: true);
            if (entity == null)
            {
                return null;
            }

            if (!entity.ZaaerId.HasValue)
            {
                var z = await _numberingService.GetNextEntityZaaerIdAsync(
                    NumberingDocCodes.Apartment,
                    "pms-property-apartment-backfill",
                    $"pms-apartment-backfill:{entity.ApartmentId}",
                    cancellationToken);
                entity.ZaaerId = ZaaerIdMapper.ToNullableInt32(z.ZaaerId);
                await _numberingService.MarkCommittedAsync(z.AuditId, cancellationToken);
            }

            await ApplyApartmentDtoAsync(entity, dto, scope, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var list = await MapApartmentsAsync(scope, new[] { entity }, cancellationToken);
            return list[0];
        }

        public async Task<bool> DeleteApartmentAsync(int id, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var entity = await ResolveApartmentAsync(id, scope, cancellationToken, tracking: true);
            if (entity == null)
            {
                return false;
            }

            entity.IsActive = false;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<IReadOnlyList<PmsFacilityListItemDto>> ListFacilitiesAsync(CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var rows = await _context.Facilities.AsNoTracking()
                .Where(f => (f.HotelId == scope.ScopeHotelId || f.HotelId == scope.LocalHotelId))
                .OrderBy(f => f.FacilityName)
                .ToListAsync(cancellationToken);

            return await MapFacilitiesAsync(scope, rows, cancellationToken);
        }

        public async Task<PmsFacilityListItemDto?> GetFacilityAsync(int id, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var entity = await ResolveFacilityAsync(id, scope, cancellationToken);
            if (entity == null)
            {
                return null;
            }

            var list = await MapFacilitiesAsync(scope, new[] { entity }, cancellationToken);
            return list.FirstOrDefault();
        }

        public async Task<PmsFacilityListItemDto> CreateFacilityAsync(PmsUpsertFacilityDto dto, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var auditIds = new List<long>();

            try
            {
                var identity = await _numberingService.GetNextEntityZaaerIdAsync(
                    NumberingDocCodes.Facility,
                    "pms-property-facility",
                    $"pms-facility:{scope.ScopeHotelId}:{Guid.NewGuid():N}",
                    cancellationToken);
                auditIds.Add(identity.AuditId);

                var now = KsaTime.Now;
                var entity = new Facility
                {
                    HotelId = scope.ScopeHotelId,
                    FacilityName = dto.FacilityName.Trim(),
                    FacilityNameEn = dto.FacilityNameEn?.Trim(),
                    Description = dto.Description?.Trim(),
                    BuildingId = dto.BuildingZaaerId,
                    FloorId = dto.FloorZaaerId,
                    IsActive = dto.IsActive,
                    ImageUrlsJson = SerializeImageUrls(dto.ImageUrls),
                    ZaaerId = ZaaerIdMapper.ToNullableInt32(identity.ZaaerId),
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.Facilities.Add(entity);
                await _context.SaveChangesAsync(cancellationToken);

                foreach (var auditId in auditIds)
                {
                    await _numberingService.MarkCommittedAsync(auditId, cancellationToken);
                }

                var list = await MapFacilitiesAsync(scope, new[] { entity }, cancellationToken);
                return list[0];
            }
            catch (Exception ex)
            {
                foreach (var auditId in auditIds)
                {
                    await _numberingService.MarkVoidedAsync(auditId, ex.Message, cancellationToken);
                }

                throw;
            }
        }

        public async Task<PmsFacilityListItemDto?> UpdateFacilityAsync(int id, PmsUpsertFacilityDto dto, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var entity = await ResolveFacilityAsync(id, scope, cancellationToken, tracking: true);
            if (entity == null)
            {
                return null;
            }

            if (!entity.ZaaerId.HasValue)
            {
                var z = await _numberingService.GetNextEntityZaaerIdAsync(
                    NumberingDocCodes.Facility,
                    "pms-property-facility-backfill",
                    $"pms-facility-backfill:{entity.FacilityId}",
                    cancellationToken);
                entity.ZaaerId = ZaaerIdMapper.ToNullableInt32(z.ZaaerId);
                await _numberingService.MarkCommittedAsync(z.AuditId, cancellationToken);
            }

            entity.FacilityName = dto.FacilityName.Trim();
            entity.FacilityNameEn = dto.FacilityNameEn?.Trim();
            entity.Description = dto.Description?.Trim();
            entity.BuildingId = dto.BuildingZaaerId;
            entity.FloorId = dto.FloorZaaerId;
            entity.IsActive = dto.IsActive;
            entity.ImageUrlsJson = SerializeImageUrls(dto.ImageUrls);
            entity.UpdatedAt = KsaTime.Now;

            await _context.SaveChangesAsync(cancellationToken);

            var list = await MapFacilitiesAsync(scope, new[] { entity }, cancellationToken);
            return list[0];
        }

        public async Task<bool> DeleteFacilityAsync(int id, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var entity = await ResolveFacilityAsync(id, scope, cancellationToken, tracking: true);
            if (entity == null)
            {
                return false;
            }

            _context.Facilities.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>
        /// Tenant property rows often use <c>hotel_settings.zaaer_id</c> in <c>hotel_id</c> (e.g. 21), not internal <c>hotel_id</c> (e.g. 1).
        /// </summary>
        private sealed record PropertyHotelScope(int LocalHotelId, int ScopeHotelId, string PropertyType);

        private async Task<List<ApartmentScopeRow>> QueryApartmentScopeRowsAsync(
            PropertyHotelScope scope,
            CancellationToken cancellationToken) =>
            await _context.Apartments.AsNoTracking()
                .Where(a => a.HotelId == scope.ScopeHotelId || a.HotelId == scope.LocalHotelId)
                .Select(a => new ApartmentScopeRow(
                    a.ApartmentId,
                    a.ZaaerId,
                    a.BuildingId,
                    a.FloorId,
                    a.RoomTypeId,
                    a.ParentApartmentId,
                    a.ApartmentCode,
                    a.ApartmentName))
                .ToListAsync(cancellationToken);

        private static IQueryable<Apartment> ProjectApartmentListRow(IQueryable<Apartment> query) =>
            query.Select(a => new Apartment
            {
                ApartmentId = a.ApartmentId,
                HotelId = a.HotelId,
                ZaaerId = a.ZaaerId,
                ApartmentCode = a.ApartmentCode,
                ApartmentName = a.ApartmentName ?? a.ApartmentCode,
                Status = a.Status ?? "vacant",
                IsActive = a.IsActive ?? true,
                BuildingId = a.BuildingId,
                FloorId = a.FloorId,
                RoomTypeId = a.RoomTypeId,
                ParentApartmentId = a.ParentApartmentId,
                KitchenType = a.KitchenType,
                HallType = a.HallType,
                ResortAreaType = a.ResortAreaType,
                BathroomsCount = a.BathroomsCount ?? 0,
                SingleBedsCount = a.SingleBedsCount ?? 0,
                DoubleBedsCount = a.DoubleBedsCount ?? 0,
                Area = a.Area,
                TelephoneExtension = a.TelephoneExtension,
                Description = a.Description,
                ServicesJson = a.ServicesJson,
                FacilitiesJson = a.FacilitiesJson
            });

        private async Task<PropertyHotelScope> GetCurrentHotelScopeAsync(CancellationToken cancellationToken)
        {
            var tenant = _tenantService.GetTenant()
                ?? throw new UnauthorizedAccessException("Tenant not resolved. Provide X-Hotel-Code.");

            var code = tenant.Code.Trim();
            var hotel = await _context.HotelSettings.AsNoTracking()
                .Where(h => h.HotelCode != null)
                .FirstOrDefaultAsync(
                    h => h.HotelCode!.ToLower() == code.ToLower(),
                    cancellationToken)
                ?? throw new InvalidOperationException($"HotelSettings not found for code: {tenant.Code}");

            if (!hotel.ZaaerId.HasValue)
            {
                return new PropertyHotelScope(hotel.HotelId, hotel.HotelId, NormalizePropertyType(hotel.PropertyType));
            }

            return new PropertyHotelScope(hotel.HotelId, hotel.ZaaerId.Value, NormalizePropertyType(hotel.PropertyType));
        }

        private async Task<Building?> ResolveBuildingAsync(
            int id,
            PropertyHotelScope scope,
            CancellationToken cancellationToken,
            bool tracking = false)
        {
            var query = tracking ? _context.Buildings : _context.Buildings.AsNoTracking();
            return await query.FirstOrDefaultAsync(
                b => (b.HotelId == scope.ScopeHotelId || b.HotelId == scope.LocalHotelId) && (b.BuildingId == id || b.ZaaerId == id),
                cancellationToken);
        }

        private async Task<RoomType?> ResolveRoomTypeAsync(
            int id,
            PropertyHotelScope scope,
            CancellationToken cancellationToken,
            bool tracking = false)
        {
            var query = tracking ? _context.RoomTypes : _context.RoomTypes.AsNoTracking();
            return await query.FirstOrDefaultAsync(
                rt => (rt.HotelId == scope.ScopeHotelId || rt.HotelId == scope.LocalHotelId) && (rt.RoomTypeId == id || rt.ZaaerId == id),
                cancellationToken);
        }

        private async Task<Apartment?> ResolveApartmentAsync(
            int id,
            PropertyHotelScope scope,
            CancellationToken cancellationToken,
            bool tracking = false)
        {
            var query = tracking ? _context.Apartments : _context.Apartments.AsNoTracking();
            return await query.FirstOrDefaultAsync(
                a => (a.HotelId == scope.ScopeHotelId || a.HotelId == scope.LocalHotelId) && (a.ApartmentId == id || a.ZaaerId == id),
                cancellationToken);
        }

        private async Task<Facility?> ResolveFacilityAsync(
            int id,
            PropertyHotelScope scope,
            CancellationToken cancellationToken,
            bool tracking = false)
        {
            var query = tracking ? _context.Facilities : _context.Facilities.AsNoTracking();
            return await query.FirstOrDefaultAsync(
                f => (f.HotelId == scope.ScopeHotelId || f.HotelId == scope.LocalHotelId) && (f.FacilityId == id || f.ZaaerId == id),
                cancellationToken);
        }

        private async Task SyncBuildingFloorsAsync(
            Building building,
            PropertyHotelScope scope,
            int buildingLinkId,
            List<PmsPropertyFloorLineDto> floorDtos,
            List<long> auditIds,
            CancellationToken cancellationToken)
        {
            var existingFloors = await _context.Floors
                .Where(f => (f.HotelId == scope.ScopeHotelId || f.HotelId == scope.LocalHotelId))
                .ToListAsync(cancellationToken);

            var buildingFloors = PropertyEntityLinks.FloorsForBuilding(existingFloors, building).ToList();
            var incomingKeys = floorDtos
                .Select(f => f.FloorId ?? f.ZaaerId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();

            foreach (var remove in buildingFloors.Where(f =>
                         !incomingKeys.Contains(f.FloorId) &&
                         !(f.ZaaerId.HasValue && incomingKeys.Contains(f.ZaaerId.Value))))
            {
                var hasUnitsOnFloor = await _context.Apartments.AsNoTracking()
                    .Where(a => (a.HotelId == scope.ScopeHotelId || a.HotelId == scope.LocalHotelId))
                    .AnyAsync(a =>
                        a.FloorId == remove.FloorId ||
                        (remove.ZaaerId.HasValue && a.FloorId == remove.ZaaerId),
                        cancellationToken);

                if (hasUnitsOnFloor)
                {
                    throw new InvalidOperationException(
                        $"Cannot remove floor '{remove.FloorName}' while units are assigned.");
                }

                _context.Floors.Remove(remove);
            }

            var order = 0;
            foreach (var line in floorDtos.OrderBy(f => f.SortOrder).ThenBy(f => f.FloorNumber))
            {
                order++;
                Floor? existing = null;
                if (line.FloorId.HasValue)
                {
                    existing = buildingFloors.FirstOrDefault(f => f.FloorId == line.FloorId.Value);
                }

                if (existing == null && line.ZaaerId.HasValue)
                {
                    existing = buildingFloors.FirstOrDefault(f => f.ZaaerId == line.ZaaerId.Value);
                }

                if (existing != null)
                {
                    existing.FloorNumber = line.FloorNumber;
                    existing.FloorName = line.FloorName?.Trim() ?? string.Empty;
                    existing.SortOrder = line.SortOrder > 0 ? line.SortOrder : order;
                    existing.IsActive = line.IsActive;
                    existing.BuildingId = buildingLinkId;
                    if (line.ZaaerId.HasValue)
                    {
                        existing.ZaaerId = line.ZaaerId;
                    }
                }
                else
                {
                    var floorZaaer = await _numberingService.GetNextEntityZaaerIdAsync(
                        NumberingDocCodes.Floor,
                        "pms-property-floor",
                        $"pms-floor:{building.BuildingId}:{Guid.NewGuid():N}",
                        cancellationToken);
                    auditIds.Add(floorZaaer.AuditId);

                    var newFloor = new Floor
                    {
                        HotelId = scope.ScopeHotelId,
                        BuildingId = buildingLinkId,
                        FloorNumber = line.FloorNumber,
                        FloorName = line.FloorName?.Trim() ?? string.Empty,
                        SortOrder = line.SortOrder > 0 ? line.SortOrder : order,
                        IsActive = line.IsActive,
                        ZaaerId = ZaaerIdMapper.ToNullableInt32(floorZaaer.ZaaerId)
                    };
                    _context.Floors.Add(newFloor);
                    buildingFloors.Add(newFloor);
                }
            }
        }

        private async Task<PmsBuildingDetailDto> MapBuildingDetailAsync(Building building, CancellationToken cancellationToken)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var floors = await _context.Floors.AsNoTracking()
                .Where(f => (f.HotelId == scope.ScopeHotelId || f.HotelId == scope.LocalHotelId))
                .ToListAsync(cancellationToken);

            var apartmentLinks = await QueryApartmentScopeRowsAsync(scope, cancellationToken);

            var buildingFloors = PropertyEntityLinks.FloorsForBuilding(floors, building)
                .OrderBy(f => f.SortOrder)
                .ThenBy(f => f.FloorNumber)
                .Select(f => new PmsPropertyFloorLineDto
                {
                    FloorId = f.FloorId,
                    ZaaerId = f.ZaaerId,
                    FloorNumber = f.FloorNumber,
                    FloorName = f.FloorName,
                    SortOrder = f.SortOrder,
                    IsActive = f.IsActive
                })
                .ToList();

            return new PmsBuildingDetailDto
            {
                BuildingId = building.BuildingId,
                ZaaerId = building.ZaaerId,
                BuildingName = building.BuildingName ?? string.Empty,
                Description = building.Description ?? building.Address,
                IsActive = building.IsActive,
                FloorCount = buildingFloors.Count,
                ApartmentCount = apartmentLinks.Count(a => PropertyEntityLinks.ApartmentReferencesBuilding(a.BuildingId, building)),
                Floors = buildingFloors
            };
        }

        private static PmsRoomTypeListItemDto MapRoomType(RoomType rt, IEnumerable<ApartmentScopeRow> apartments)
        {
            var linkId = PropertyEntityLinks.GetRoomTypeLinkId(rt);
            return new PmsRoomTypeListItemDto
            {
                RoomTypeId = rt.RoomTypeId,
                ZaaerId = rt.ZaaerId,
                RoomTypeName = rt.RoomTypeName,
                RoomTypeNameEn = rt.RoomTypeNameEn,
                RoomTypeDesc = rt.RoomTypeDesc,
                RoomCategory = rt.RoomCategory,
                RoomCount = rt.RoomCount,
                SortOrder = rt.SortOrder,
                IsActive = rt.IsActive,
                ImageUrl = rt.ImageUrl,
                ApartmentCount = apartments.Count(a =>
                    a.RoomTypeId == rt.RoomTypeId || (linkId.HasValue && a.RoomTypeId == linkId.Value))
            };
        }

        private async Task<Apartment> MapApartmentEntityAsync(
            PmsUpsertApartmentDto dto,
            PropertyHotelScope scope,
            CancellationToken cancellationToken)
        {
            var entity = new Apartment
            {
                HotelId = scope.ScopeHotelId,
                ApartmentCode = dto.ApartmentCode.Trim(),
                ApartmentName = dto.ApartmentName?.Trim() ?? dto.ApartmentCode.Trim(),
                Status = string.IsNullOrWhiteSpace(dto.Status) ? "vacant" : dto.Status,
                IsActive = dto.IsActive
            };

            await ApplyApartmentDtoAsync(entity, dto, scope, cancellationToken);
            return entity;
        }

        private async Task ApplyApartmentDtoAsync(
            Apartment entity,
            PmsUpsertApartmentDto dto,
            PropertyHotelScope scope,
            CancellationToken cancellationToken)
        {
            entity.ApartmentCode = dto.ApartmentCode.Trim();
            entity.ApartmentName = dto.ApartmentName?.Trim() ?? dto.ApartmentCode.Trim();
            entity.Status = string.IsNullOrWhiteSpace(dto.Status) ? "vacant" : dto.Status;
            entity.IsActive = dto.IsActive;
            entity.TelephoneExtension = dto.TelephoneExtension?.Trim();
            entity.BathroomsCount = dto.BathroomsCount;
            entity.KitchenType = dto.KitchenType;
            entity.HallType = dto.HallType;
            entity.ResortAreaType = NormalizeResortAreaType(dto.ResortAreaType);
            entity.SingleBedsCount = dto.SingleBedsCount;
            entity.DoubleBedsCount = dto.DoubleBedsCount;
            entity.Area = dto.Area;
            entity.Description = dto.Description?.Trim();
            entity.ServicesJson = dto.Services == null || dto.Services.Count == 0
                ? null
                : JsonSerializer.Serialize(dto.Services, JsonOptions);

            entity.FacilitiesJson = dto.FacilityZaaerIds == null || dto.FacilityZaaerIds.Count == 0
                ? null
                : JsonSerializer.Serialize(
                    dto.FacilityZaaerIds.Where(id => id >= 0).Distinct().ToList(),
                    JsonOptions);

            entity.BuildingId = dto.BuildingZaaerId;
            entity.FloorId = dto.FloorZaaerId;
            entity.ParentApartmentId = await ResolveParentApartmentLinkIdAsync(dto.ParentApartmentZaaerId, entity.ApartmentId, scope, cancellationToken);

            if (dto.RoomTypeZaaerId.HasValue)
            {
                var rt = await _context.RoomTypes.AsNoTracking()
                    .FirstOrDefaultAsync(
                        r => (r.HotelId == scope.ScopeHotelId || r.HotelId == scope.LocalHotelId) &&
                             (r.ZaaerId == dto.RoomTypeZaaerId || r.RoomTypeId == dto.RoomTypeZaaerId),
                        cancellationToken);

                entity.RoomTypeId = rt == null
                    ? dto.RoomTypeZaaerId
                    : PropertyEntityLinks.GetRoomTypeLinkId(rt);
            }
            else
            {
                entity.RoomTypeId = null;
            }
        }

        private async Task<IReadOnlyList<PmsApartmentListItemDto>> MapApartmentsAsync(
            PropertyHotelScope scope,
            IReadOnlyList<Apartment> rows,
            CancellationToken cancellationToken)
        {
            var buildings = await _context.Buildings.AsNoTracking()
                .Where(b => (b.HotelId == scope.ScopeHotelId || b.HotelId == scope.LocalHotelId))
                .ToListAsync(cancellationToken);

            var floors = await _context.Floors.AsNoTracking()
                .Where(f => (f.HotelId == scope.ScopeHotelId || f.HotelId == scope.LocalHotelId))
                .ToListAsync(cancellationToken);

            var roomTypes = await _context.RoomTypes.AsNoTracking()
                .Where(rt => (rt.HotelId == scope.ScopeHotelId || rt.HotelId == scope.LocalHotelId))
                .ToListAsync(cancellationToken);

            var allApartments = await QueryApartmentScopeRowsAsync(scope, cancellationToken);

            var apartmentLinks = allApartments
                .Select(a => new
                {
                    a.ApartmentId,
                    a.ZaaerId,
                    LinkId = a.ZaaerId ?? a.ApartmentId,
                    Name = string.IsNullOrWhiteSpace(a.ApartmentName) ? a.ApartmentCode : a.ApartmentName
                })
                .ToList();

            var childCounts = allApartments
                .Where(a => a.ParentApartmentId.HasValue)
                .GroupBy(a => a.ParentApartmentId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            return rows.Select(a =>
            {
                var building = buildings.FirstOrDefault(b =>
                    a.BuildingId == b.BuildingId || (b.ZaaerId.HasValue && a.BuildingId == b.ZaaerId));

                var floor = floors.FirstOrDefault(f =>
                    a.FloorId == f.FloorId || (f.ZaaerId.HasValue && a.FloorId == f.ZaaerId));

                var roomType = roomTypes.FirstOrDefault(rt =>
                    a.RoomTypeId == rt.RoomTypeId || (rt.ZaaerId.HasValue && a.RoomTypeId == rt.ZaaerId));

                var parent = a.ParentApartmentId.HasValue
                    ? apartmentLinks.FirstOrDefault(p => p.ApartmentId == a.ParentApartmentId.Value || p.ZaaerId == a.ParentApartmentId.Value || p.LinkId == a.ParentApartmentId.Value)
                    : null;
                var childCountKeys = new[] { a.ZaaerId, a.ApartmentId }
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .Distinct();

                return new PmsApartmentListItemDto
                {
                    ApartmentId = a.ApartmentId,
                    ZaaerId = a.ZaaerId,
                    ApartmentCode = a.ApartmentCode,
                    ApartmentName = a.ApartmentName,
                    Status = a.Status,
                    IsActive = a.IsActive ?? true,
                    BuildingZaaerId = building?.ZaaerId ?? a.BuildingId,
                    BuildingName = building?.BuildingName,
                    FloorZaaerId = floor?.ZaaerId ?? a.FloorId,
                    FloorName = floor?.FloorName,
                    RoomTypeZaaerId = roomType?.ZaaerId ?? a.RoomTypeId,
                    RoomTypeName = roomType?.RoomTypeName,
                    RoomCategory = roomType?.RoomCategory,
                    ParentApartmentZaaerId = parent?.LinkId ?? a.ParentApartmentId,
                    ParentApartmentName = parent?.Name,
                    ChildUnitCount = childCountKeys.Sum(key => childCounts.GetValueOrDefault(key)),
                    KitchenType = a.KitchenType,
                    HallType = a.HallType,
                    ResortAreaType = a.ResortAreaType,
                    BathroomsCount = a.BathroomsCount ?? 0,
                    SingleBedsCount = a.SingleBedsCount ?? 0,
                    DoubleBedsCount = a.DoubleBedsCount ?? 0,
                    Area = a.Area,
                    TelephoneExtension = a.TelephoneExtension,
                    Description = a.Description,
                    Services = DeserializeServices(a.ServicesJson),
                    FacilityZaaerIds = DeserializeFacilityZaaerIds(a.FacilitiesJson)
                };
            }).ToList();
        }

        private async Task<IReadOnlyList<PmsFacilityListItemDto>> MapFacilitiesAsync(
            PropertyHotelScope scope,
            IReadOnlyList<Facility> rows,
            CancellationToken cancellationToken)
        {
            var buildings = await _context.Buildings.AsNoTracking()
                .Where(b => (b.HotelId == scope.ScopeHotelId || b.HotelId == scope.LocalHotelId))
                .ToListAsync(cancellationToken);

            var floors = await _context.Floors.AsNoTracking()
                .Where(f => (f.HotelId == scope.ScopeHotelId || f.HotelId == scope.LocalHotelId))
                .ToListAsync(cancellationToken);

            return rows.Select(f =>
            {
                var building = buildings.FirstOrDefault(b =>
                    f.BuildingId == b.BuildingId || (b.ZaaerId.HasValue && f.BuildingId == b.ZaaerId));

                var floor = floors.FirstOrDefault(fl =>
                    f.FloorId == fl.FloorId || (fl.ZaaerId.HasValue && f.FloorId == fl.ZaaerId));

                return new PmsFacilityListItemDto
                {
                    FacilityId = f.FacilityId,
                    ZaaerId = f.ZaaerId,
                    FacilityName = f.FacilityName,
                    FacilityNameEn = f.FacilityNameEn,
                    Description = f.Description,
                    BuildingZaaerId = building?.ZaaerId ?? f.BuildingId,
                    BuildingName = building?.BuildingName,
                    FloorZaaerId = floor?.ZaaerId ?? f.FloorId,
                    FloorName = floor?.FloorName,
                    IsActive = f.IsActive,
                    ImageUrls = DeserializeImageUrls(f.ImageUrlsJson)
                };
            }).ToList();
        }

        private static string NormalizePropertyType(string? value)
        {
            var normalized = value?.Trim().ToLowerInvariant();
            return PropertyTypes.All.Contains(normalized) ? normalized! : PropertyTypes.Hotel;
        }

        private async Task<int?> ResolveParentApartmentLinkIdAsync(
            int? parentApartmentId,
            int currentApartmentId,
            PropertyHotelScope scope,
            CancellationToken cancellationToken)
        {
            if (!parentApartmentId.HasValue)
            {
                return null;
            }

            var parent = await _context.Apartments.AsNoTracking()
                .FirstOrDefaultAsync(
                    a => (a.HotelId == scope.ScopeHotelId || a.HotelId == scope.LocalHotelId) &&
                         (a.ApartmentId == parentApartmentId.Value || a.ZaaerId == parentApartmentId.Value),
                    cancellationToken);

            if (parent == null)
            {
                throw new ArgumentException("Parent chalet was not found.");
            }

            if (currentApartmentId > 0 && parent.ApartmentId == currentApartmentId)
            {
                throw new ArgumentException("A unit cannot be assigned as its own parent chalet.");
            }

            return parent.ZaaerId ?? parent.ApartmentId;
        }

        private static string NormalizeRoomCategory(string? value)
        {
            var normalized = value?.Trim().ToLowerInvariant();
            return PropertyRoomCategories.All.Contains(normalized) ? normalized! : PropertyRoomCategories.Other;
        }

        private static string? NormalizeResortAreaType(string? value)
        {
            var normalized = value?.Trim().ToLowerInvariant();
            return ResortAreaTypes.All.Contains(normalized) ? normalized : null;
        }

        private static List<string> DeserializeServices(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<string>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static List<int> DeserializeFacilityZaaerIds(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<int>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<int>>(json, JsonOptions) ?? new List<int>();
            }
            catch
            {
                return new List<int>();
            }
        }

        private static List<string> DeserializeImageUrls(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<string>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static string? SerializeImageUrls(List<string>? urls)
        {
            if (urls == null || urls.Count == 0)
            {
                return null;
            }

            return JsonSerializer.Serialize(
                urls.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).ToList(),
                JsonOptions);
        }

        private static PmsPropertyModeLabelsDto BuildPropertyModeLabels(string mode)
        {
            if (PropertyTypes.IsHall(mode))
            {
                return new PmsPropertyModeLabelsDto
                {
                    UnitLabelEn = "Hall",
                    UnitLabelAr = "قاعة",
                    UnitTypeLabelEn = "Hall Categories",
                    UnitTypeLabelAr = "فئات القاعات",
                    BoardTitleEn = "Hall Operations",
                    BoardTitleAr = "تشغيل القاعات"
                };
            }

            if (PropertyTypes.IsResort(mode))
            {
                return new PmsPropertyModeLabelsDto
                {
                    UnitLabelEn = "Chalet",
                    UnitLabelAr = "شاليه",
                    UnitTypeLabelEn = "Chalet Types",
                    UnitTypeLabelAr = "أنواع الشاليهات",
                    BoardTitleEn = "Resort Board",
                    BoardTitleAr = "لوحة المنتجع"
                };
            }

            return new PmsPropertyModeLabelsDto();
        }
    }
}
