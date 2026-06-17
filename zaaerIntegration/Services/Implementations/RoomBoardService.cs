#pragma warning disable CS1591

using System.Globalization;
using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.RoomBoard;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    public sealed class RoomBoardService : IRoomBoardService
    {
        private const string StatusAvailable = "available";
        private const string StatusOccupied = "occupied";
        private const string StatusReserved = "reserved";
        private const string StatusCleaning = "cleaning";
        private const string StatusMaintenance = "maintenance";

        private readonly ApplicationDbContext _context;
        private readonly ILogger<RoomBoardService> _logger;

        public RoomBoardService(ApplicationDbContext context, ILogger<RoomBoardService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<RoomBoardResponseDto> GetRoomBoardAsync(
            RoomBoardRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var fromDate = request.EffectiveFromDate;
            var toDate = request.EffectiveToDate < fromDate ? fromDate : request.EffectiveToDate;
            var toDateExclusive = toDate.AddDays(1);
            var boardDate = request.Date?.Date ?? fromDate;
            var boardDateExclusive = boardDate.AddDays(1);

            var rooms = await GetRoomsAsync(request, cancellationToken);
            var colorRows = await GetRoomCardColorRowsAsync(request, cancellationToken);
            var reservations = await GetReservationRowsAsync(request, fromDate, toDateExclusive, cancellationToken);
            var maintenances = await GetMaintenanceRowsAsync(request, fromDate, toDateExclusive, cancellationToken);
            var lookups = await GetLookupsAsync(request, cancellationToken);

            var reservationsByRoom = reservations
                .GroupBy(x => x.ApartmentId)
                .ToDictionary(x => x.Key, x => x.OrderBy(r => r.CheckInDate).ToList());

            var maintenanceByRoom = maintenances
                .GroupBy(x => x.ApartmentId)
                .ToDictionary(x => x.Key, x => x.OrderBy(m => m.FromDate).ToList());

            var colorsByRoom = colorRows
                .GroupBy(x => x.ApartmentZaaerId)
                .ToDictionary(x => x.Key, x => x.OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt).First());

            var mixedRentalByReservation = await LoadMixedRentalPeriodFlagsAsync(
                reservations.Select(x => x.ReservationId).Distinct().ToList(),
                cancellationToken);

            var allRoomItems = rooms
                .Select(room => BuildRoomItem(
                    room,
                    reservationsByRoom,
                    maintenanceByRoom,
                    colorsByRoom,
                    mixedRentalByReservation,
                    boardDate,
                    boardDateExclusive))
                .OrderBy(item => item.BuildingName)
                .ThenBy(item => item.FloorName)
                .ThenBy(item => item.ApartmentCode)
                .ToList();

            var roomItems = allRoomItems
                .Where(item => MatchesStatusFilter(item, request))
                .Where(item => MatchesAlertFilter(item, request.Alert))
                .ToList();

            var visibleRoomIds = roomItems.Select(x => x.ApartmentId).ToHashSet();
            var apartmentBoardIdMap = BuildApartmentBoardIdMap(allRoomItems);
            var calendarItems = BuildCalendarItems(reservations, maintenances)
                .Select(item => NormalizeCalendarItemApartmentId(item, apartmentBoardIdMap))
                .Where(item => item != null)
                .Select(item => item!)
                .Where(x => visibleRoomIds.Contains(x.ApartmentId))
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .ToList();

            _logger.LogDebug(
                "Loaded room board. Rooms={RoomCount}, Reservations={ReservationCount}, Maintenances={MaintenanceCount}, From={FromDate}, To={ToDate}",
                roomItems.Count,
                reservations.Count,
                maintenances.Count,
                fromDate,
                toDate);

            return new RoomBoardResponseDto
            {
                Summary = BuildSummary(allRoomItems),
                Lookups = lookups,
                Rooms = roomItems,
                CalendarItems = calendarItems
            };
        }

        public async Task<(bool Ok, string? ErrorCode)> ApplyApartmentQuickStateAsync(
            int apartmentRouteId,
            int? hotelId,
            string mode,
            CancellationToken cancellationToken = default)
        {
            var normalizedMode = NormalizeQuickMode(mode);
            if (normalizedMode is null)
            {
                return (false, "INVALID_MODE");
            }

            var apartment = await ResolveApartmentByBoardRouteIdAsync(apartmentRouteId, hotelId, tracked: true, cancellationToken);

            if (apartment == null)
            {
                return (false, "NOT_FOUND");
            }

            switch (normalizedMode)
            {
                case "clearmaintenance":
                    // Only removes maintenance rows; apartment.status is not modified (board derives maintenance from maintenances).
                    await CancelActiveMaintenancesForApartmentAsync(apartment, cancellationToken);
                    break;

                case "setcleaning":
                    // Room board: "under cleaning" means the unit needs housekeeping — persist as dirty (not workflow "cleaning").
                    apartment.HousekeepingStatus = "dirty";
                    break;

                case "clearcleaning":
                    apartment.HousekeepingStatus = "clean";
                    break;

                default:
                    return (false, "INVALID_MODE");
            }

            await _context.SaveChangesAsync(cancellationToken);
            return (true, null);
        }

        private static string? NormalizeQuickMode(string? mode)
        {
            var s = (mode ?? string.Empty).Trim().ToLowerInvariant().Replace("_", "").Replace("-", "");
            return s switch
            {
                "setcleaning" => "setcleaning",
                "clearcleaning" => "clearcleaning",
                "clearmaintenance" => "clearmaintenance",
                _ => null
            };
        }

        private async Task CancelActiveMaintenancesForApartmentAsync(Apartment apartment, CancellationToken cancellationToken)
        {
            var boardUnitId = GetMaintenanceStorageUnitId(apartment);
            var rows = await _context.Maintenances
                .Where(m =>
                    m.HotelId == apartment.HotelId &&
                    (m.UnitId == boardUnitId ||
                     m.UnitId == apartment.ApartmentId ||
                     (apartment.ZaaerId.HasValue && m.UnitId == apartment.ZaaerId.Value)))
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                if (!IsActiveMaintenanceRecordStatus(row.Status))
                {
                    continue;
                }

                _context.Maintenances.Remove(row);
            }
        }

        private static bool IsActiveMaintenanceRecordStatus(string? status)
        {
            var normalized = NormalizeStatus(status);
            return normalized is "" or "active" or StatusMaintenance or "open" or "inprogress" or "in-progress";
        }

        public async Task<(bool Found, IReadOnlyList<RoomBoardMaintenanceRowDto> Rows)> GetApartmentMaintenancesAsync(
            int apartmentRouteId,
            int? hotelId,
            CancellationToken cancellationToken = default)
        {
            var apt = await ResolveApartmentByBoardRouteIdAsync(apartmentRouteId, hotelId, tracked: false, cancellationToken);

            if (apt == null)
            {
                return (false, Array.Empty<RoomBoardMaintenanceRowDto>());
            }

            var boardUnitId = GetMaintenanceStorageUnitId(apt);
            var rows = await _context.Maintenances
                .AsNoTracking()
                .Where(m =>
                    m.HotelId == apt.HotelId &&
                    (m.UnitId == boardUnitId ||
                     m.UnitId == apt.ApartmentId ||
                     (apt.ZaaerId.HasValue && m.UnitId == apt.ZaaerId.Value)))
                .OrderByDescending(m => m.FromDate)
                .ThenByDescending(m => m.Id)
                .Take(200)
                .Select(m => new RoomBoardMaintenanceRowDto
                {
                    Id = m.Id,
                    FromDate = m.FromDate,
                    ToDate = m.ToDate,
                    Reason = m.Reason,
                    Comment = m.Comment,
                    Categories = ParseMaintenanceCategories(m.MaintenanceCategories),
                    Status = m.Status ?? string.Empty
                })
                .ToListAsync(cancellationToken);

            return (true, rows);
        }

        public async Task<(bool Ok, string? ErrorCode, int? MaintenanceId)> CreateApartmentMaintenanceAsync(
            int apartmentRouteId,
            int? hotelId,
            RoomBoardMaintenanceCreateRequestDto dto,
            int userId,
            CancellationToken cancellationToken = default)
        {
            var apt = await LoadApartmentTrackedForBoardAsync(apartmentRouteId, hotelId, cancellationToken);
            if (apt == null)
            {
                return (false, "NOT_FOUND", null);
            }

            if (NormalizeStatus(apt.Status) == "rented")
            {
                return (false, "MAINTENANCE_NOT_ALLOWED_RENTED", null);
            }

            if (!TryParseLocalDate(dto.FromDate, out var from) || !TryParseLocalDate(dto.ToDate, out var to))
            {
                return (false, "BAD_DATES", null);
            }

            from = from.Date;
            to = to.Date;
            if (from > to)
            {
                return (false, "BAD_DATE_RANGE", null);
            }

            var reason = NormalizeMaintenanceReason(dto.Reason);
            var comment = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment.Trim();
            var categories = SerializeMaintenanceCategories(dto.Categories);
            var effectiveUserId = userId > 0 ? userId : 0;

            var maintenance = new Maintenance
            {
                HotelId = apt.HotelId,
                UnitId = GetMaintenanceStorageUnitId(apt),
                UserId = effectiveUserId,
                FromDate = from,
                ToDate = to,
                Reason = reason,
                Comment = comment,
                MaintenanceCategories = categories,
                Status = "active",
                CreatedAt = KsaTime.Now
            };

            _context.Maintenances.Add(maintenance);
            await _context.SaveChangesAsync(cancellationToken);
            return (true, null, maintenance.Id);
        }

        public async Task<(bool Ok, string? ErrorCode)> UpdateApartmentMaintenanceAsync(
            int apartmentRouteId,
            int? hotelId,
            int maintenanceId,
            RoomBoardMaintenanceUpdateRequestDto dto,
            CancellationToken cancellationToken = default)
        {
            var apt = await LoadApartmentTrackedForBoardAsync(apartmentRouteId, hotelId, cancellationToken);
            if (apt == null)
            {
                return (false, "NOT_FOUND");
            }

            var row = await MaintenancesForApartmentQuery(apt)
                .FirstOrDefaultAsync(m => m.Id == maintenanceId, cancellationToken);

            if (row == null)
            {
                return (false, "MAINTENANCE_NOT_FOUND");
            }

            if (!IsActiveMaintenanceRecordStatus(row.Status))
            {
                return (false, "MAINTENANCE_NOT_EDITABLE");
            }

            if (!TryParseLocalDate(dto.FromDate, out var from) || !TryParseLocalDate(dto.ToDate, out var to))
            {
                return (false, "BAD_DATES");
            }

            from = from.Date;
            to = to.Date;
            if (from > to)
            {
                return (false, "BAD_DATE_RANGE");
            }

            row.FromDate = from;
            row.ToDate = to;
            row.Reason = NormalizeMaintenanceReason(dto.Reason);
            row.Comment = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment.Trim();
            row.MaintenanceCategories = SerializeMaintenanceCategories(dto.Categories);
            row.UnitId = GetMaintenanceStorageUnitId(apt);
            row.UpdatedAt = KsaTime.Now;

            await _context.SaveChangesAsync(cancellationToken);
            return (true, null);
        }

        public async Task<(bool Ok, string? ErrorCode)> CancelApartmentMaintenanceAsync(
            int apartmentRouteId,
            int? hotelId,
            int maintenanceId,
            CancellationToken cancellationToken = default)
        {
            var apt = await LoadApartmentTrackedForBoardAsync(apartmentRouteId, hotelId, cancellationToken);
            if (apt == null)
            {
                return (false, "NOT_FOUND");
            }

            var row = await MaintenancesForApartmentQuery(apt)
                .FirstOrDefaultAsync(m => m.Id == maintenanceId, cancellationToken);

            if (row == null)
            {
                return (false, "MAINTENANCE_NOT_FOUND");
            }

            if (!IsActiveMaintenanceRecordStatus(row.Status))
            {
                return (false, "MAINTENANCE_NOT_CANCELABLE");
            }

            _context.Maintenances.Remove(row);

            await _context.SaveChangesAsync(cancellationToken);
            return (true, null);
        }

        /// <summary>
        /// Resolves the apartment for a room-board route id (board id = <c>ZaaerId ?? ApartmentId</c>).
        /// Prefers <see cref="Apartment.ZaaerId"/> so a route value like 2063 matches the Zaaer-linked room,
        /// not another row whose internal <c>apartment_id</c> happens to equal 2063.
        /// When <paramref name="hotelId"/> is set, resolution is scoped to that hotel.
        /// </summary>
        private async Task<Apartment?> ResolveApartmentByBoardRouteIdAsync(
            int apartmentRouteId,
            int? hotelId,
            bool tracked,
            CancellationToken cancellationToken)
        {
            IQueryable<Apartment> query = tracked ? _context.Apartments : _context.Apartments.AsNoTracking();

            if (hotelId.HasValue)
            {
                query = query.Where(a => a.HotelId == hotelId.Value);
            }

            var byZaaer = await query.FirstOrDefaultAsync(a => a.ZaaerId == apartmentRouteId, cancellationToken);
            if (byZaaer != null)
            {
                return byZaaer;
            }

            return await query.FirstOrDefaultAsync(a => a.ApartmentId == apartmentRouteId, cancellationToken);
        }

        private async Task<Apartment?> LoadApartmentTrackedForBoardAsync(int apartmentRouteId, int? hotelId, CancellationToken ct)
        {
            return await ResolveApartmentByBoardRouteIdAsync(apartmentRouteId, hotelId, tracked: true, ct);
        }

        /// <summary>
        /// Value stored in <c>maintenances.unit_id</c>: Zaaer/board id when present, otherwise internal <c>apartment_id</c> (matches room card <c>apartmentId</c>).
        /// </summary>
        private static int GetMaintenanceStorageUnitId(Apartment apt) => apt.ZaaerId ?? apt.ApartmentId;

        private IQueryable<Maintenance> MaintenancesForApartmentQuery(Apartment apt)
        {
            var boardUnitId = GetMaintenanceStorageUnitId(apt);
            return _context.Maintenances.Where(m =>
                m.HotelId == apt.HotelId &&
                (m.UnitId == boardUnitId ||
                 m.UnitId == apt.ApartmentId ||
                 (apt.ZaaerId.HasValue && m.UnitId == apt.ZaaerId.Value)));
        }

        private static bool TryParseLocalDate(string? s, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(s))
            {
                return false;
            }

            var trimmed = s.Trim();
            if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                return true;
            }

            return DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
        }

        private static string NormalizeMaintenanceReason(string? r)
        {
            var x = NormalizeStatus(r).Replace("-", "");
            return x switch
            {
                "maintenance" => "maintenance",
                "staffshortage" or "staff_shortage" => "staff_shortage",
                "ownerrequest" or "owner_request" => "owner_request",
                "other" => "other",
                _ => "other"
            };
        }

        private static readonly HashSet<string> AllowedMaintenanceCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            "ac",
            "water_heater",
            "plumbing",
            "electrical",
            "paint",
            "flooring",
            "doors_locks",
            "furniture",
            "appliances",
            "kitchen",
            "bathroom",
            "pest_control",
            "deep_cleaning",
            "wifi",
            "other"
        };

        private static string? NormalizeMaintenanceCategoryKey(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var x = raw.Trim().ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
            return x switch
            {
                "ac" or "acs" or "air_conditioning" or "airconditioning" or "hvac" => "ac",
                "water_heater" or "waterheater" or "heater" or "geyser" => "water_heater",
                "plumbing" or "plumber" or "pipes" => "plumbing",
                "electrical" or "electric" or "electricity" or "wiring" => "electrical",
                "paint" or "paints" or "painting" => "paint",
                "flooring" or "floor" or "floors" or "tiles" or "tiling" => "flooring",
                "doors_locks" or "doorslocks" or "door" or "doors" or "lock" or "locks" => "doors_locks",
                "furniture" or "furnishing" or "furnishings" => "furniture",
                "appliances" or "appliance" or "white_goods" => "appliances",
                "kitchen" or "kitchenette" => "kitchen",
                "bathroom" or "bath" or "sanitary" => "bathroom",
                "pest_control" or "pestcontrol" or "pest" => "pest_control",
                "deep_cleaning" or "deepcleaning" or "deep_clean" => "deep_cleaning",
                "wifi" or "wi_fi" or "internet" or "network" => "wifi",
                "other" => "other",
                _ when AllowedMaintenanceCategories.Contains(x) => x,
                _ => null
            };
        }

        private static string? SerializeMaintenanceCategories(IReadOnlyList<string>? categories)
        {
            if (categories == null || categories.Count == 0)
            {
                return null;
            }

            var normalized = categories
                .Select(NormalizeMaintenanceCategoryKey)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return normalized.Count == 0 ? null : string.Join(",", normalized);
        }

        private static IReadOnlyList<string> ParseMaintenanceCategories(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<string>();
            }

            return raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeMaintenanceCategoryKey)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(x => x!)
                .ToList();
        }

        private async Task<RoomBoardLookupsDto> GetLookupsAsync(RoomBoardRequestDto request, CancellationToken cancellationToken)
        {
            var buildingsQuery = _context.Buildings.AsNoTracking();
            var floorsQuery = _context.Floors.AsNoTracking();
            var roomTypesQuery = _context.RoomTypes.AsNoTracking();

            if (request.HotelId.HasValue)
            {
                buildingsQuery = buildingsQuery.Where(x => x.HotelId == request.HotelId.Value);
                floorsQuery = floorsQuery.Where(x => x.HotelId == request.HotelId.Value);
                roomTypesQuery = roomTypesQuery.Where(x => x.HotelId == request.HotelId.Value);
            }

            var buildings = await buildingsQuery
                .Where(x => !string.IsNullOrWhiteSpace(x.BuildingName))
                .OrderBy(x => x.BuildingName)
                .Select(x => new RoomBoardLookupDto
                {
                    Id = x.ZaaerId ?? x.BuildingId,
                    Text = x.BuildingName
                })
                .ToListAsync(cancellationToken);

            var floors = await floorsQuery
                .Where(x => !string.IsNullOrWhiteSpace(x.FloorName))
                .OrderBy(x => x.FloorNumber)
                .ThenBy(x => x.FloorName)
                .Select(x => new RoomBoardLookupDto
                {
                    Id = x.ZaaerId ?? x.FloorId,
                    Text = x.FloorName
                })
                .ToListAsync(cancellationToken);

            var roomTypes = await roomTypesQuery
                .Where(x => !string.IsNullOrWhiteSpace(x.RoomTypeName))
                .OrderBy(x => x.RoomTypeName)
                .Select(x => new RoomBoardLookupDto
                {
                    Id = x.ZaaerId ?? x.RoomTypeId,
                    Text = x.RoomTypeName
                })
                .ToListAsync(cancellationToken);

            return new RoomBoardLookupsDto
            {
                Buildings = buildings,
                Floors = floors,
                RoomTypes = roomTypes
            };
        }

        private async Task<List<RoomRow>> GetRoomsAsync(RoomBoardRequestDto request, CancellationToken cancellationToken)
        {
            var buildingIds = ParseIds(request.BuildingIds);
            var floorIds = ParseIds(request.FloorIds);
            var roomTypeIds = ParseIds(request.RoomTypeIds);
            var isResort = await IsResortPropertyAsync(request.HotelId, cancellationToken);
            var apartmentsQuery = _context.Apartments.AsNoTracking();

            if (isResort)
            {
                apartmentsQuery = apartmentsQuery.Where(a => !a.ParentApartmentId.HasValue);
            }

            var query =
                from apartment in apartmentsQuery
                from building in _context.Buildings.AsNoTracking()
                    .Where(x =>
                        x.HotelId == apartment.HotelId &&
                        (apartment.BuildingId == x.BuildingId || apartment.BuildingId == x.ZaaerId))
                    .OrderBy(x => apartment.BuildingId == x.BuildingId ? 0 : 1)
                    .ThenBy(x => x.BuildingId)
                    .Take(1)
                    .DefaultIfEmpty()
                from floor in _context.Floors.AsNoTracking()
                    .Where(x =>
                        x.HotelId == apartment.HotelId &&
                        (apartment.FloorId == x.FloorId || apartment.FloorId == x.ZaaerId))
                    .OrderBy(x => apartment.FloorId == x.FloorId ? 0 : 1)
                    .ThenBy(x => x.FloorId)
                    .Take(1)
                    .DefaultIfEmpty()
                from roomType in _context.RoomTypes.AsNoTracking()
                    .Where(x =>
                        x.HotelId == apartment.HotelId &&
                        (apartment.RoomTypeId == x.RoomTypeId || apartment.RoomTypeId == x.ZaaerId))
                    .OrderBy(x => apartment.RoomTypeId == x.RoomTypeId ? 0 : 1)
                    .ThenBy(x => x.RoomTypeId)
                    .Take(1)
                    .DefaultIfEmpty()
                let apartmentBoardId = apartment.ZaaerId ?? apartment.ApartmentId
                select new RoomRow
                {
                    ApartmentId = apartmentBoardId,
                    InternalApartmentId = apartment.ApartmentId,
                    HotelId = apartment.HotelId,
                    BuildingId = building != null ? building.ZaaerId ?? building.BuildingId : apartment.BuildingId,
                    BuildingName = building != null ? building.BuildingName : null,
                    FloorId = floor != null ? floor.ZaaerId ?? floor.FloorId : apartment.FloorId,
                    FloorName = floor != null ? floor.FloorName : null,
                    RoomTypeId = roomType != null ? roomType.ZaaerId ?? roomType.RoomTypeId : apartment.RoomTypeId,
                    RoomTypeName = roomType != null ? roomType.RoomTypeName : null,
                    ApartmentCode = apartment.ApartmentCode,
                    ApartmentName = apartment.ApartmentName,
                    ApartmentStatus = apartment.Status,
                    HousekeepingStatus = apartment.HousekeepingStatus
                };

            if (request.HotelId.HasValue)
            {
                query = query.Where(x => x.HotelId == request.HotelId.Value);
            }

            if (buildingIds.Count > 0)
            {
                query = query.Where(x => x.BuildingId.HasValue && buildingIds.Contains(x.BuildingId.Value));
            }
            else if (request.BuildingId.HasValue)
            {
                query = query.Where(x => x.BuildingId == request.BuildingId.Value);
            }

            if (floorIds.Count > 0)
            {
                query = query.Where(x => x.FloorId.HasValue && floorIds.Contains(x.FloorId.Value));
            }
            else if (request.FloorId.HasValue)
            {
                query = query.Where(x => x.FloorId == request.FloorId.Value);
            }

            if (roomTypeIds.Count > 0)
            {
                query = query.Where(x => x.RoomTypeId.HasValue && roomTypeIds.Contains(x.RoomTypeId.Value));
            }
            else if (request.RoomTypeId.HasValue)
            {
                query = query.Where(x => x.RoomTypeId == request.RoomTypeId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = request.Search.Trim();
                query = query.Where(x =>
                    x.ApartmentCode.Contains(search) ||
                    (x.ApartmentName != null && x.ApartmentName.Contains(search)) ||
                    (x.BuildingName != null && x.BuildingName.Contains(search)) ||
                    (x.FloorName != null && x.FloorName.Contains(search)) ||
                    (x.RoomTypeName != null && x.RoomTypeName.Contains(search)));
            }

            var rows = await query.ToListAsync(cancellationToken);

            // Keep this as a last-resort guard; deterministic lookup joins above prevent normal fan-out.
            return rows
                .GroupBy(x => x.ApartmentId)
                .Select(g => g
                    .OrderByDescending(x => !string.IsNullOrWhiteSpace(x.ApartmentName))
                    .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.RoomTypeName))
                    .First())
                .ToList();
        }

        private async Task<bool> IsResortPropertyAsync(int? hotelId, CancellationToken cancellationToken)
        {
            var query = _context.HotelSettings.AsNoTracking();

            if (hotelId.HasValue)
            {
                query = query.Where(h => h.HotelId == hotelId.Value || h.ZaaerId == hotelId.Value);
            }

            var propertyType = await query
                .Select(h => h.PropertyType)
                .FirstOrDefaultAsync(cancellationToken);

            return PropertyTypes.IsResort(propertyType);
        }

        private async Task<List<RoomColorRow>> GetRoomCardColorRowsAsync(
            RoomBoardRequestDto request,
            CancellationToken cancellationToken)
        {
            var query = _context.RoomCardColorSettings
                .AsNoTracking()
                .Where(x => x.IsActive);

            if (request.HotelId.HasValue)
            {
                query = query.Where(x => x.HotelId == request.HotelId.Value);
            }

            return await query
                .Select(x => new RoomColorRow
                {
                    ApartmentZaaerId = x.ApartmentZaaerId,
                    OccupiedGuestBackColor = x.OccupiedGuestBackColor,
                    OccupiedTextColor = x.OccupiedTextColor,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync(cancellationToken);
        }

        private async Task<List<ReservationRow>> GetReservationRowsAsync(
            RoomBoardRequestDto request,
            DateTime fromDate,
            DateTime toDateExclusive,
            CancellationToken cancellationToken)
        {
            var unitRows = await _context.ReservationUnits.AsNoTracking()
                .Where(unit =>
                    unit.CheckInDate < toDateExclusive &&
                    ((unit.DepartureDate ?? unit.CheckOutDate) >= fromDate ||
                     unit.Status == "checked_in" ||
                     unit.Status == "checkedin" ||
                     unit.Status == "CheckedIn" ||
                     unit.Status == "checked-in" ||
                     unit.Status == "occupied"))
                .Select(unit => new
                {
                    unit.UnitId,
                    unit.ReservationId,
                    unit.ApartmentId,
                    unit.CheckInDate,
                    unit.CheckOutDate,
                    unit.DepartureDate,
                    unit.Status
                })
                .ToListAsync(cancellationToken);

            if (unitRows.Count == 0)
            {
                return new List<ReservationRow>();
            }

            var reservationStorageIds = unitRows
                .Select(unit => unit.ReservationId)
                .Distinct()
                .ToList();

            var reservationsQuery = _context.Reservations.AsNoTracking()
                .Where(reservation =>
                    reservationStorageIds.Contains(reservation.ReservationId) ||
                    (reservation.ZaaerId.HasValue && reservationStorageIds.Contains(reservation.ZaaerId.Value)));

            if (request.HotelId.HasValue)
            {
                reservationsQuery = reservationsQuery.Where(reservation => reservation.HotelId == request.HotelId.Value);
            }

            var reservationRows = await reservationsQuery
                .Select(reservation => new ReservationLookupRow
                {
                    ReservationId = reservation.ReservationId,
                    ZaaerId = reservation.ZaaerId,
                    HotelId = reservation.HotelId,
                    ReservationNo = reservation.ReservationNo,
                    CustomerId = reservation.CustomerId,
                    BalanceAmount = reservation.BalanceAmount,
                    RentalType = reservation.RentalType,
                    Status = reservation.Status
                })
                .ToListAsync(cancellationToken);

            if (reservationRows.Count == 0)
            {
                return new List<ReservationRow>();
            }

            var reservationsByStorageId = new Dictionary<int, List<ReservationLookupRow>>();
            foreach (var reservation in reservationRows)
            {
                AddLookupRow(reservationsByStorageId, reservation.ReservationId, reservation);
                if (reservation.ZaaerId is > 0)
                {
                    AddLookupRow(reservationsByStorageId, reservation.ZaaerId.Value, reservation);
                }
            }

            var customerStorageIds = reservationRows
                .Where(reservation => reservation.CustomerId.HasValue)
                .Select(reservation => reservation.CustomerId!.Value)
                .Distinct()
                .ToList();

            var customersByStorageId = new Dictionary<int, List<CustomerLookupRow>>();
            if (customerStorageIds.Count > 0)
            {
                var customerRows = await _context.Customers.AsNoTracking()
                    .Where(customer =>
                        customerStorageIds.Contains(customer.CustomerId) ||
                        (customer.ZaaerId.HasValue && customerStorageIds.Contains(customer.ZaaerId.Value)))
                    .Select(customer => new CustomerLookupRow
                    {
                        CustomerId = customer.CustomerId,
                        ZaaerId = customer.ZaaerId,
                        CustomerName = customer.CustomerName
                    })
                    .ToListAsync(cancellationToken);

                foreach (var customer in customerRows)
                {
                    AddLookupRow(customersByStorageId, customer.CustomerId, customer);
                    if (customer.ZaaerId is > 0)
                    {
                        AddLookupRow(customersByStorageId, customer.ZaaerId.Value, customer);
                    }
                }
            }

            var apartmentStorageIds = unitRows
                .Select(unit => unit.ApartmentId)
                .Distinct()
                .ToList();
            var hotelIds = reservationRows
                .Select(reservation => reservation.HotelId)
                .Distinct()
                .ToList();

            var apartmentRows = await _context.Apartments.AsNoTracking()
                .Where(apartment =>
                    hotelIds.Contains(apartment.HotelId) &&
                    (apartmentStorageIds.Contains(apartment.ApartmentId) ||
                     (apartment.ZaaerId.HasValue && apartmentStorageIds.Contains(apartment.ZaaerId.Value))))
                .Select(apartment => new ApartmentLookupRow
                {
                    HotelId = apartment.HotelId,
                    ApartmentId = apartment.ApartmentId,
                    ZaaerId = apartment.ZaaerId
                })
                .ToListAsync(cancellationToken);

            var apartmentsByHotelAndStorageId = new Dictionary<(int HotelId, int StorageId), List<ApartmentLookupRow>>();
            foreach (var apartment in apartmentRows)
            {
                AddLookupRow(apartmentsByHotelAndStorageId, (apartment.HotelId, apartment.ApartmentId), apartment);
                if (apartment.ZaaerId is > 0)
                {
                    AddLookupRow(apartmentsByHotelAndStorageId, (apartment.HotelId, apartment.ZaaerId.Value), apartment);
                }
            }

            var rows = new List<ReservationRow>();
            foreach (var unit in unitRows)
            {
                if (!reservationsByStorageId.TryGetValue(unit.ReservationId, out var reservationMatches))
                {
                    continue;
                }

                var reservation = reservationMatches
                    .OrderBy(match => unit.ReservationId == match.ReservationId ? 0 : 1)
                    .ThenBy(match => match.ReservationId)
                    .First();

                var customerName = (string?)null;
                var customerStorageId = reservation.CustomerId;
                if (customerStorageId.HasValue)
                {
                    if (customersByStorageId.TryGetValue(customerStorageId.Value, out var customerMatchList))
                    {
                        var customer = customerMatchList
                            .OrderBy(match => customerStorageId.Value == match.CustomerId ? 0 : 1)
                            .ThenBy(match => match.CustomerId)
                            .First();
                        customerName = customer.CustomerName;
                    }
                }

                var apartmentId = unit.ApartmentId;
                if (apartmentsByHotelAndStorageId.TryGetValue((reservation.HotelId, unit.ApartmentId), out var apartmentMatches))
                {
                    var apartment = apartmentMatches
                        .OrderBy(match => unit.ApartmentId == match.ApartmentId ? 0 : 1)
                        .ThenBy(match => match.ApartmentId)
                        .First();
                    apartmentId = apartment.ZaaerId ?? apartment.ApartmentId;
                }

                rows.Add(new ReservationRow
                {
                    UnitId = unit.UnitId,
                    ReservationId = reservation.ZaaerId ?? reservation.ReservationId,
                    ApartmentId = apartmentId,
                    HotelId = reservation.HotelId,
                    ReservationNo = reservation.ReservationNo,
                    CustomerId = reservation.CustomerId,
                    CustomerName = customerName,
                    CheckInDate = unit.CheckInDate,
                    CheckOutDate = unit.CheckOutDate,
                    DepartureDate = unit.DepartureDate,
                    BalanceAmount = reservation.BalanceAmount ?? 0m,
                    RentalType = reservation.RentalType,
                    UnitStatus = unit.Status,
                    ReservationStatus = reservation.Status
                });
            }

            return rows
                .GroupBy(x => x.UnitId)
                .Select(g => g
                    .OrderByDescending(x => !string.IsNullOrWhiteSpace(x.CustomerName))
                    .ThenBy(x => x.ReservationId)
                    .First())
                .ToList();
        }

        private async Task<List<MaintenanceRow>> GetMaintenanceRowsAsync(
            RoomBoardRequestDto request,
            DateTime fromDate,
            DateTime toDateExclusive,
            CancellationToken cancellationToken)
        {
            var query = _context.Maintenances
                .AsNoTracking()
                .Where(x => x.FromDate < toDateExclusive && x.ToDate >= fromDate);

            if (request.HotelId.HasValue)
            {
                query = query.Where(x => x.HotelId == request.HotelId.Value);
            }

            var rows = await query
                .Select(m => new
                {
                    m.Id,
                    m.UnitId,
                    m.HotelId,
                    m.FromDate,
                    m.ToDate,
                    m.Reason,
                    m.Comment,
                    m.MaintenanceCategories,
                    m.Status
                })
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                return new List<MaintenanceRow>();
            }

            var maintenanceUnitIds = rows
                .Select(x => x.UnitId)
                .Distinct()
                .ToList();

            var hotelIds = rows
                .Select(x => x.HotelId)
                .Distinct()
                .ToList();

            var apartmentRows = await _context.Apartments.AsNoTracking()
                .Where(a =>
                    hotelIds.Contains(a.HotelId) &&
                    (maintenanceUnitIds.Contains(a.ApartmentId) ||
                     (a.ZaaerId.HasValue && maintenanceUnitIds.Contains(a.ZaaerId.Value))))
                .Select(a => new
                {
                    a.HotelId,
                    a.ApartmentId,
                    a.ZaaerId
                })
                .ToListAsync(cancellationToken);

            var apartmentByHotelAndStorageId = new Dictionary<(int HotelId, int StorageId), int>();
            foreach (var apartment in apartmentRows)
            {
                var boardId = apartment.ZaaerId ?? apartment.ApartmentId;
                apartmentByHotelAndStorageId[(apartment.HotelId, apartment.ApartmentId)] = boardId;
                if (apartment.ZaaerId is > 0)
                {
                    apartmentByHotelAndStorageId[(apartment.HotelId, apartment.ZaaerId.Value)] = boardId;
                }
            }

            return rows
                .Select(m => new MaintenanceRow
                {
                    Id = m.Id,
                    ApartmentId = apartmentByHotelAndStorageId.TryGetValue((m.HotelId, m.UnitId), out var apartmentId)
                        ? apartmentId
                        : m.UnitId,
                    HotelId = m.HotelId,
                    FromDate = m.FromDate,
                    ToDate = m.ToDate,
                    Reason = m.Reason,
                    Comment = m.Comment,
                    Categories = m.MaintenanceCategories,
                    Status = m.Status
                })
                .ToList();
        }

        private async Task<Dictionary<int, bool>> LoadMixedRentalPeriodFlagsAsync(
            IReadOnlyList<int> reservationIds,
            CancellationToken cancellationToken)
        {
            if (reservationIds.Count == 0)
            {
                return new Dictionary<int, bool>();
            }

            var rows = await _context.ReservationPeriods.AsNoTracking()
                .Where(p => reservationIds.Contains(p.ReservationId))
                .Select(p => new { p.ReservationId, p.RentalType })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(x => x.ReservationId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .Select(x => x.RentalType)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count() > 1);
        }

        private static RoomBoardItemDto BuildRoomItem(
            RoomRow room,
            IReadOnlyDictionary<int, List<ReservationRow>> reservationsByRoom,
            IReadOnlyDictionary<int, List<MaintenanceRow>> maintenanceByRoom,
            IReadOnlyDictionary<int, RoomColorRow> colorsByRoom,
            IReadOnlyDictionary<int, bool> mixedRentalByReservation,
            DateTime boardDate,
            DateTime boardDateExclusive)
        {
            var roomReservations = GetRowsForRoom(reservationsByRoom, room.ApartmentId, room.InternalApartmentId);
            var roomMaintenances = GetRowsForRoom(maintenanceByRoom, room.ApartmentId, room.InternalApartmentId);

            var currentMaintenance = roomMaintenances?.FirstOrDefault(x => IsActiveMaintenance(x, boardDate, boardDateExclusive));
            var calendarStay = roomReservations?.FirstOrDefault(x => IsCurrentStay(x, boardDate, boardDateExclusive));
            var physicalStay = roomReservations?
                .Where(x => IsPhysicalStay(x, boardDate))
                .OrderByDescending(x => x.CheckInDate)
                .FirstOrDefault();
            var displayStay = physicalStay ?? calendarStay;
            var nextStay = roomReservations?.FirstOrDefault(x => x.CheckInDate >= boardDateExclusive && IsReservationStatus(x));

            var operationalStatus = ResolveOperationalStatus(room, currentMaintenance, displayStay, nextStay);
            colorsByRoom.TryGetValue(room.ApartmentId, out var colors);
            var isOverstay = physicalStay != null && IsOverstayStay(physicalStay, boardDate);
            var overstayDays = isOverstay && physicalStay != null
                ? ComputeOverstayDays(physicalStay, boardDate)
                : 0;
            var isDepartureToday = displayStay != null &&
                                   !isOverstay &&
                                   GetStayEndDate(displayStay).Date == boardDate.Date;
            var hasUnpaidBalance = displayStay != null && displayStay.BalanceAmount > 0;
            var hasMixedRentalPeriods = displayStay != null &&
                                        mixedRentalByReservation.TryGetValue(displayStay.ReservationId, out var mixed) &&
                                        mixed;

            return new RoomBoardItemDto
            {
                ApartmentId = room.ApartmentId,
                HotelId = room.HotelId,
                InternalApartmentId = room.InternalApartmentId,
                ApartmentCode = room.ApartmentCode,
                ApartmentName = room.ApartmentName,
                OperationalStatus = operationalStatus,
                StatusCssClass = ToStatusCssClass(operationalStatus),
                ApartmentStatus = NormalizeStatus(room.ApartmentStatus),
                HousekeepingStatus = NormalizeStatus(room.HousekeepingStatus),
                IsDepartureToday = isDepartureToday,
                IsOverstay = isOverstay,
                OverstayDays = overstayDays,
                HasUnpaidBalance = hasUnpaidBalance,
                StatusType = !isDepartureToday && !isOverstay,
                CheckInDateShort = displayStay == null ? string.Empty : FormatShortDate(displayStay.CheckInDate),
                CheckOutDateShort = displayStay == null ? string.Empty : FormatShortDate(displayStay.CheckOutDate),
                CustomerName = displayStay?.CustomerName ?? string.Empty,
                CustomerId = displayStay?.CustomerId,
                BalanceAmount = displayStay?.BalanceAmount ?? 0m,
                RentalType = displayStay?.RentalType ?? string.Empty,
                HasMixedRentalPeriods = hasMixedRentalPeriods,
                ReservationNo = displayStay?.ReservationNo ?? string.Empty,
                ReservationStatus = displayStay?.ReservationStatus ?? string.Empty,
                OccupiedGuestBackColor = colors?.OccupiedGuestBackColor,
                OccupiedTextColor = colors?.OccupiedTextColor,
                BuildingId = room.BuildingId,
                BuildingName = room.BuildingName,
                FloorId = room.FloorId,
                FloorName = room.FloorName,
                RoomTypeId = room.RoomTypeId,
                RoomTypeName = room.RoomTypeName,
                MaintenanceReason = currentMaintenance?.Reason,
                MaintenanceCategories = ParseMaintenanceCategories(currentMaintenance?.Categories),
                MaintenanceComment = currentMaintenance?.Comment,
                MaintenanceToDateShort = currentMaintenance == null
                    ? string.Empty
                    : FormatShortDate(currentMaintenance.ToDate),
                CurrentStay = displayStay == null ? null : new CurrentStayDto
                {
                    ReservationId = displayStay.ReservationId,
                    UnitId = displayStay.UnitId,
                    ReservationNo = displayStay.ReservationNo,
                    CustomerId = displayStay.CustomerId,
                    CustomerName = displayStay.CustomerName,
                    CheckInDate = displayStay.CheckInDate,
                    CheckOutDate = displayStay.CheckOutDate,
                    DepartureDate = displayStay.DepartureDate,
                    CheckInDateShort = FormatShortDate(displayStay.CheckInDate),
                    CheckOutDateShort = FormatShortDate(displayStay.CheckOutDate),
                    BalanceAmount = displayStay.BalanceAmount,
                    RentalType = displayStay.RentalType,
                    HasMixedRentalPeriods = hasMixedRentalPeriods,
                    IsDepartureToday = isDepartureToday,
                    IsOverstay = isOverstay,
                    OverstayDays = overstayDays,
                    HasUnpaidBalance = hasUnpaidBalance,
                    StatusType = !isDepartureToday && !isOverstay,
                    Status = NormalizeStatus(displayStay.UnitStatus)
                },
                NextStay = nextStay == null ? null : new NextStayDto
                {
                    ReservationId = nextStay.ReservationId,
                    UnitId = nextStay.UnitId,
                    ReservationNo = nextStay.ReservationNo,
                    CustomerName = nextStay.CustomerName,
                    CheckInDate = nextStay.CheckInDate,
                    CheckOutDate = nextStay.CheckOutDate,
                    Status = NormalizeStatus(nextStay.UnitStatus)
                }
            };
        }

        private static string ResolveOperationalStatus(
            RoomRow room,
            MaintenanceRow? currentMaintenance,
            ReservationRow? currentStay,
            ReservationRow? nextStay)
        {
            var apartmentStatus = NormalizeStatus(room.ApartmentStatus);
            var housekeepingStatus = NormalizeStatus(room.HousekeepingStatus);

            if (currentMaintenance != null)
            {
                return StatusMaintenance;
            }

            if (currentStay != null && (IsOccupiedStatus(currentStay) || apartmentStatus == "rented"))
            {
                return StatusOccupied;
            }

            if (currentStay != null && IsReservationStatus(currentStay))
            {
                return StatusReserved;
            }

            if (IsCleaningStatus(housekeepingStatus))
            {
                return StatusCleaning;
            }

            if (nextStay != null && apartmentStatus == StatusReserved)
            {
                return StatusReserved;
            }

            return StatusAvailable;
        }

        private static Dictionary<int, int> BuildApartmentBoardIdMap(IEnumerable<RoomBoardItemDto> rooms)
        {
            var map = new Dictionary<int, int>();

            foreach (var room in rooms)
            {
                map[room.ApartmentId] = room.ApartmentId;

                if (room.InternalApartmentId.HasValue)
                {
                    map[room.InternalApartmentId.Value] = room.ApartmentId;
                }
            }

            return map;
        }

        private static RoomBoardCalendarItemDto? NormalizeCalendarItemApartmentId(
            RoomBoardCalendarItemDto item,
            IReadOnlyDictionary<int, int> apartmentBoardIdMap)
        {
            if (!apartmentBoardIdMap.TryGetValue(item.ApartmentId, out var boardApartmentId))
            {
                return null;
            }

            if (boardApartmentId == item.ApartmentId)
            {
                return item;
            }

            return new RoomBoardCalendarItemDto
            {
                Id = item.Id,
                ApartmentId = boardApartmentId,
                Text = item.Text,
                StartDate = item.StartDate,
                EndDate = item.EndDate,
                Type = item.Type,
                StatusCssClass = item.StatusCssClass,
                ReservationNo = item.ReservationNo,
                GuestName = item.GuestName,
                StatusLabel = item.StatusLabel,
                RentalType = item.RentalType,
                ReservationId = item.ReservationId,
                UnitId = item.UnitId
            };
        }

        private static List<RoomBoardCalendarItemDto> BuildCalendarItems(
            IEnumerable<ReservationRow> reservations,
            IEnumerable<MaintenanceRow> maintenances)
        {
            var reservationItems = reservations
                .Where(x => !IsInactiveReservationStatus(x.UnitStatus))
                .Select(x => new RoomBoardCalendarItemDto
                {
                    Id = $"reservation-{x.UnitId}",
                    ApartmentId = x.ApartmentId,
                    Text = string.IsNullOrWhiteSpace(x.CustomerName)
                        ? x.ReservationNo
                        : $"{x.ReservationNo} - {x.CustomerName}",
                    StartDate = x.CheckInDate,
                    EndDate = x.DepartureDate ?? x.CheckOutDate,
                    Type = NormalizeStatus(x.UnitStatus),
                    StatusCssClass = ToStatusCssClass(IsOccupiedStatus(x) ? StatusOccupied : StatusReserved),
                    ReservationNo = x.ReservationNo ?? string.Empty,
                    GuestName = x.CustomerName ?? string.Empty,
                    StatusLabel = NormalizeStatus(x.UnitStatus),
                    RentalType = x.RentalType ?? string.Empty,
                    ReservationId = x.ReservationId,
                    UnitId = x.UnitId
                });

            var maintenanceItems = maintenances
                .Where(IsActiveMaintenanceStatus)
                .Select(x => new RoomBoardCalendarItemDto
                {
                    Id = $"maintenance-{x.Id}",
                    ApartmentId = x.ApartmentId,
                    Text = string.IsNullOrWhiteSpace(x.Reason) ? "Maintenance" : x.Reason,
                    StartDate = x.FromDate,
                    EndDate = x.ToDate,
                    Type = StatusMaintenance,
                    StatusCssClass = ToStatusCssClass(StatusMaintenance)
                });

            return reservationItems
                .Concat(maintenanceItems)
                .OrderBy(x => x.StartDate)
                .ToList();
        }

        private static RoomBoardSummaryDto BuildSummary(IReadOnlyCollection<RoomBoardItemDto> rooms)
        {
            return new RoomBoardSummaryDto
            {
                Total = rooms.Count,
                Available = rooms.Count(x => x.OperationalStatus == StatusAvailable),
                Occupied = rooms.Count(x => x.OperationalStatus == StatusOccupied),
                Reserved = rooms.Count(x => x.OperationalStatus == StatusReserved),
                Cleaning = rooms.Count(x => x.OperationalStatus == StatusCleaning),
                Maintenance = rooms.Count(x => x.OperationalStatus == StatusMaintenance),
                DepartureToday = rooms.Count(x => x.IsDepartureToday),
                Overstay = rooms.Count(x => x.IsOverstay),
                UnpaidBalance = rooms.Count(x => x.HasUnpaidBalance),
                OccupiedDirty = rooms.Count(IsOccupiedDirty)
            };
        }

        private static bool IsOccupiedDirty(RoomBoardItemDto item) =>
            NormalizeStatus(item.ApartmentStatus) == "rented" &&
            NormalizeStatus(item.HousekeepingStatus) == "dirty";

        private static List<T>? GetRowsForRoom<T>(
            IReadOnlyDictionary<int, List<T>> rowsByRoom,
            int apartmentId,
            int internalApartmentId)
        {
            if (rowsByRoom.TryGetValue(apartmentId, out var rows))
            {
                return rows;
            }

            return internalApartmentId != apartmentId && rowsByRoom.TryGetValue(internalApartmentId, out var internalRows)
                ? internalRows
                : null;
        }

        private static string FormatShortDate(DateTime date)
        {
            return date.ToString("dd/MM/yyyy");
        }

        private static HashSet<int> ParseIds(string? value)
        {
            return (value ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => int.TryParse(x, out var id) ? id : (int?)null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToHashSet();
        }

        private static void AddLookupRow<TKey, TValue>(
            IDictionary<TKey, List<TValue>> lookup,
            TKey key,
            TValue row)
            where TKey : notnull
        {
            if (!lookup.TryGetValue(key, out var rows))
            {
                rows = new List<TValue>();
                lookup[key] = rows;
            }

            rows.Add(row);
        }

        private static HashSet<string> ParseStatuses(string? value)
        {
            return (value ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeStatus)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static DateTime GetStayEndDate(ReservationRow row) =>
            (row.DepartureDate ?? row.CheckOutDate).Date;

        private static bool IsPhysicalStay(ReservationRow row, DateTime boardDate) =>
            IsOccupiedStatus(row) &&
            !IsInactiveReservationStatus(row.UnitStatus) &&
            row.CheckInDate.Date <= boardDate.Date;

        private static bool IsOverstayStay(ReservationRow row, DateTime boardDate) =>
            IsPhysicalStay(row, boardDate) && GetStayEndDate(row) < boardDate.Date;

        private static int ComputeOverstayDays(ReservationRow row, DateTime boardDate) =>
            Math.Max(1, (boardDate.Date - GetStayEndDate(row)).Days);

        private static bool IsCurrentStay(ReservationRow row, DateTime boardDate, DateTime boardDateExclusive)
        {
            var endDate = row.DepartureDate ?? row.CheckOutDate;
            return row.CheckInDate < boardDateExclusive &&
                   endDate >= boardDate &&
                   !IsInactiveReservationStatus(row.UnitStatus);
        }

        private static bool IsActiveMaintenance(MaintenanceRow row, DateTime boardDate, DateTime boardDateExclusive)
        {
            return row.FromDate < boardDateExclusive &&
                   row.ToDate >= boardDate &&
                   IsActiveMaintenanceStatus(row);
        }

        private static bool IsActiveMaintenanceStatus(MaintenanceRow row) =>
            IsActiveMaintenanceRecordStatus(row.Status);

        private static bool IsOccupiedStatus(ReservationRow row)
        {
            var status = NormalizeStatus(row.UnitStatus);
            return status is "checkedin" or "checked-in" or "occupied";
        }

        private static bool IsReservationStatus(ReservationRow row)
        {
            var status = NormalizeStatus(row.UnitStatus);
            return status is StatusReserved or "confirmed" or "unconfirmed";
        }

        private static bool IsInactiveReservationStatus(string? status)
        {
            var normalized = NormalizeStatus(status);
            return normalized is "cancelled" or "canceled" or "checkedout" or "checked-out" or "noshow" or "no-show";
        }

        private static bool IsCleaningStatus(string? status)
        {
            var normalized = NormalizeStatus(status);
            return normalized is StatusCleaning or "dirty" or "inspected" or "inspection" or "pendingcleaning" or "pending-cleaning";
        }

        private static bool MatchesStatusFilter(RoomBoardItemDto item, string? status)
        {
            return string.IsNullOrWhiteSpace(status) ||
                   item.OperationalStatus.Equals(NormalizeStatus(status), StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesStatusFilter(RoomBoardItemDto item, RoomBoardRequestDto request)
        {
            var statuses = ParseStatuses(request.Statuses);

            return statuses.Count > 0
                ? statuses.Contains(item.OperationalStatus, StringComparer.OrdinalIgnoreCase)
                : MatchesStatusFilter(item, request.Status);
        }

        private static bool MatchesAlertFilter(RoomBoardItemDto item, string? alert)
        {
            return NormalizeStatus(alert) switch
            {
                "departure-today" => item.IsDepartureToday,
                "overstay" => item.IsOverstay,
                "unpaid-balance" => item.HasUnpaidBalance,
                "occupied-dirty" => IsOccupiedDirty(item),
                _ => true
            };
        }

        private static string ToStatusCssClass(string status)
        {
            return NormalizeStatus(status) switch
            {
                StatusOccupied => "status-occupied",
                "rented" => "status-occupied",
                StatusReserved => "status-reserved",
                "confirmed" => "status-reserved",
                "unconfirmed" => "status-reserved",
                StatusCleaning => "status-cleaning",
                "dirty" => "status-cleaning",
                StatusMaintenance => "status-maintenance",
                _ => "status-available"
            };
        }

        private static string NormalizeStatus(string? status)
        {
            var normalized = (status ?? string.Empty).Trim().ToLowerInvariant().Replace("_", "-");

            return normalized switch
            {
                "vacant" => StatusAvailable,
                "free" => StatusAvailable,
                "rented" => "rented",
                "checked-in" => "checkedin",
                "checked-out" => "checkedout",
                "no-show" => "noshow",
                "in-progress" => "inprogress",
                _ => normalized
            };
        }

        private sealed class RoomRow
        {
            public int ApartmentId { get; init; }
            public int InternalApartmentId { get; init; }
            public int HotelId { get; init; }
            public int? BuildingId { get; init; }
            public string? BuildingName { get; init; }
            public int? FloorId { get; init; }
            public string? FloorName { get; init; }
            public int? RoomTypeId { get; init; }
            public string? RoomTypeName { get; init; }
            public string ApartmentCode { get; init; } = string.Empty;
            public string? ApartmentName { get; init; }
            public string? ApartmentStatus { get; init; }
            public string? HousekeepingStatus { get; init; }
        }

        private sealed class RoomColorRow
        {
            public int ApartmentZaaerId { get; init; }
            public string? OccupiedGuestBackColor { get; init; }
            public string? OccupiedTextColor { get; init; }
            public DateTime CreatedAt { get; init; }
            public DateTime? UpdatedAt { get; init; }
        }

        private sealed class ReservationLookupRow
        {
            public int ReservationId { get; init; }
            public int? ZaaerId { get; init; }
            public int HotelId { get; init; }
            public string ReservationNo { get; init; } = string.Empty;
            public int? CustomerId { get; init; }
            public decimal? BalanceAmount { get; init; }
            public string RentalType { get; init; } = string.Empty;
            public string? Status { get; init; }
        }

        private sealed class CustomerLookupRow
        {
            public int CustomerId { get; init; }
            public int? ZaaerId { get; init; }
            public string? CustomerName { get; init; }
        }

        private sealed class ApartmentLookupRow
        {
            public int HotelId { get; init; }
            public int ApartmentId { get; init; }
            public int? ZaaerId { get; init; }
        }

        private sealed class ReservationRow
        {
            public int UnitId { get; init; }
            public int ReservationId { get; init; }
            public int ApartmentId { get; init; }
            public int HotelId { get; init; }
            public string ReservationNo { get; init; } = string.Empty;
            public int? CustomerId { get; init; }
            public string? CustomerName { get; init; }
            public DateTime CheckInDate { get; init; }
            public DateTime CheckOutDate { get; init; }
            public DateTime? DepartureDate { get; init; }
            public decimal BalanceAmount { get; init; }
            public string RentalType { get; init; } = string.Empty;
            public string? UnitStatus { get; init; }
            public string? ReservationStatus { get; init; }
        }

        private sealed class MaintenanceRow
        {
            public int Id { get; init; }
            public int ApartmentId { get; init; }
            public int HotelId { get; init; }
            public DateTime FromDate { get; init; }
            public DateTime ToDate { get; init; }
            public string Reason { get; init; } = string.Empty;
            public string? Comment { get; init; }
            public string? Categories { get; init; }
            public string? Status { get; init; }
        }
    }
}
