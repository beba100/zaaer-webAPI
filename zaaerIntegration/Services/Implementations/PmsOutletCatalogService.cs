using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Security;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    public sealed class PmsOutletCatalogService : IPmsOutletCatalogService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITenantService _tenantService;
        private readonly ICurrentUserContext _currentUser;

        public PmsOutletCatalogService(
            ApplicationDbContext context,
            ITenantService tenantService,
            ICurrentUserContext currentUser)
        {
            _context = context;
            _tenantService = tenantService;
            _currentUser = currentUser;
        }

        public Task<int> ResolveCurrentHotelIdAsync(CancellationToken cancellationToken = default) =>
            GetCurrentHotelIdAsync(cancellationToken);

        private async Task<int> GetCurrentHotelIdAsync(CancellationToken cancellationToken)
        {
            var tenant = _tenantService.GetTenant()
                ?? throw new UnauthorizedAccessException("Tenant not resolved. Provide X-Hotel-Code.");

            var hotel = await _context.HotelSettings.AsNoTracking()
                .FirstOrDefaultAsync(h => h.HotelCode == tenant.Code, cancellationToken)
                ?? throw new InvalidOperationException($"HotelSettings not found for code: {tenant.Code}");

            return hotel.HotelId;
        }

        private int? ResolveCreatedBy() => PmsCurrentUser.ResolveUserId(_currentUser);

        public async Task<IReadOnlyList<PmsOutletDto>> ListOutletsAsync(CancellationToken cancellationToken = default)
        {
            var hotelId = await GetCurrentHotelIdAsync(cancellationToken);
            var rows = await _context.Outlets.AsNoTracking()
                .Where(o => o.HotelId == hotelId)
                .OrderBy(o => o.OutletName)
                .Select(o => new
                {
                    o.OutletId,
                    o.HotelId,
                    o.OutletName,
                    o.OutletNameAr,
                    o.Location,
                    o.ImageUrl,
                    o.Status,
                    o.IsActive,
                    ItemCount = o.OutletItems.Count(i => i.IsActive),
                    TableCount = o.OutletTables.Count(t => t.IsActive)
                })
                .ToListAsync(cancellationToken);

            return rows.Select(r => new PmsOutletDto
            {
                OutletId = r.OutletId,
                HotelId = r.HotelId,
                OutletName = r.OutletName,
                OutletNameAr = r.OutletNameAr,
                Location = r.Location,
                ImageUrl = r.ImageUrl,
                Status = r.Status,
                IsActive = r.IsActive,
                ItemCount = r.ItemCount,
                TableCount = r.TableCount
            }).ToList();
        }

        public async Task<PmsOutletDto?> GetOutletAsync(int outletId, CancellationToken cancellationToken = default)
        {
            var list = await ListOutletsAsync(cancellationToken);
            return list.FirstOrDefault(o => o.OutletId == outletId);
        }

        public async Task<PmsOutletDto> CreateOutletAsync(PmsUpsertOutletDto dto, CancellationToken cancellationToken = default)
        {
            var hotelId = await GetCurrentHotelIdAsync(cancellationToken);
            var entity = new Outlet
            {
                HotelId = hotelId,
                OutletName = dto.OutletName.Trim(),
                OutletNameAr = dto.OutletNameAr?.Trim(),
                Location = dto.Location?.Trim(),
                ImageUrl = dto.ImageUrl?.Trim(),
                Status = string.IsNullOrWhiteSpace(dto.Status) ? "Open" : dto.Status.Trim(),
                IsActive = dto.IsActive,
                CreatedBy = ResolveCreatedBy(),
                CreatedAt = KsaTime.Now
            };

            await _context.Outlets.AddAsync(entity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return (await GetOutletAsync(entity.OutletId, cancellationToken))!;
        }

        public async Task<PmsOutletDto?> UpdateOutletAsync(int outletId, PmsUpsertOutletDto dto, CancellationToken cancellationToken = default)
        {
            var hotelId = await GetCurrentHotelIdAsync(cancellationToken);
            var entity = await _context.Outlets.FirstOrDefaultAsync(o => o.OutletId == outletId && o.HotelId == hotelId, cancellationToken);
            if (entity == null)
            {
                return null;
            }

            entity.OutletName = dto.OutletName.Trim();
            entity.OutletNameAr = dto.OutletNameAr?.Trim();
            entity.Location = dto.Location?.Trim();
            entity.ImageUrl = dto.ImageUrl?.Trim();
            entity.Status = string.IsNullOrWhiteSpace(dto.Status) ? entity.Status : dto.Status.Trim();
            entity.IsActive = dto.IsActive;
            entity.UpdatedAt = KsaTime.Now;
            await _context.SaveChangesAsync(cancellationToken);
            return await GetOutletAsync(outletId, cancellationToken);
        }

        public async Task<IReadOnlyList<PmsOutletCategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken = default)
        {
            var hotelId = await GetCurrentHotelIdAsync(cancellationToken);
            var rows = await _context.OutletCategories.AsNoTracking()
                .Where(c => c.HotelId == hotelId)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.CategoryName)
                .Select(c => new PmsOutletCategoryDto
                {
                    CategoryId = c.CategoryId,
                    HotelId = c.HotelId,
                    CategoryName = c.CategoryName,
                    CategoryNameAr = c.CategoryNameAr,
                    Description = c.Description,
                    SortOrder = c.SortOrder,
                    IsActive = c.IsActive,
                    ItemCount = _context.OutletItems.Count(i => i.CategoryId == c.CategoryId && i.IsActive)
                })
                .ToListAsync(cancellationToken);

            return rows;
        }

        public async Task<PmsOutletCategoryDto> CreateCategoryAsync(PmsUpsertOutletCategoryDto dto, CancellationToken cancellationToken = default)
        {
            var hotelId = await GetCurrentHotelIdAsync(cancellationToken);
            var entity = new OutletCategory
            {
                HotelId = hotelId,
                CategoryName = dto.CategoryName.Trim(),
                CategoryNameAr = dto.CategoryNameAr?.Trim(),
                Description = dto.Description?.Trim(),
                SortOrder = dto.SortOrder,
                IsActive = dto.IsActive,
                CreatedBy = ResolveCreatedBy(),
                CreatedAt = KsaTime.Now
            };

            await _context.OutletCategories.AddAsync(entity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            var list = await ListCategoriesAsync(cancellationToken);
            return list.First(c => c.CategoryId == entity.CategoryId);
        }

        public async Task<PmsOutletCategoryDto?> UpdateCategoryAsync(int categoryId, PmsUpsertOutletCategoryDto dto, CancellationToken cancellationToken = default)
        {
            var hotelId = await GetCurrentHotelIdAsync(cancellationToken);
            var entity = await _context.OutletCategories.FirstOrDefaultAsync(c => c.CategoryId == categoryId && c.HotelId == hotelId, cancellationToken);
            if (entity == null)
            {
                return null;
            }

            entity.CategoryName = dto.CategoryName.Trim();
            entity.CategoryNameAr = dto.CategoryNameAr?.Trim();
            entity.Description = dto.Description?.Trim();
            entity.SortOrder = dto.SortOrder;
            entity.IsActive = dto.IsActive;
            entity.UpdatedAt = KsaTime.Now;
            await _context.SaveChangesAsync(cancellationToken);
            var list = await ListCategoriesAsync(cancellationToken);
            return list.FirstOrDefault(c => c.CategoryId == categoryId);
        }

        public async Task<IReadOnlyList<PmsOutletItemDto>> ListItemsAsync(int? outletId, int? categoryId, CancellationToken cancellationToken = default)
        {
            var hotelId = await GetCurrentHotelIdAsync(cancellationToken);
            var q = _context.OutletItems.AsNoTracking().Where(i => i.HotelId == hotelId);
            if (outletId.HasValue)
            {
                q = q.Where(i => i.OutletId == outletId.Value || i.OutletId == null);
            }

            if (categoryId.HasValue)
            {
                q = q.Where(i => i.CategoryId == categoryId.Value);
            }

            var rows = await q
                .OrderBy(i => i.ItemName)
                .Select(i => new PmsOutletItemDto
                {
                    ItemId = i.ItemId,
                    HotelId = i.HotelId,
                    OutletId = i.OutletId,
                    OutletName = i.Outlet != null ? i.Outlet.OutletName : null,
                    CategoryId = i.CategoryId,
                    CategoryName = i.OutletCategory != null ? i.OutletCategory.CategoryName : null,
                    ItemCode = i.ItemCode,
                    ItemName = i.ItemName,
                    ItemNameAr = i.ItemNameAr,
                    Description = i.Description,
                    Price = i.Price,
                    Quantity = i.Quantity,
                    ImageUrl = i.ImageUrl,
                    IncludesTax = i.IncludesTax,
                    IsActive = i.IsActive
                })
                .ToListAsync(cancellationToken);

            return rows;
        }

        public async Task<PmsOutletItemDto> CreateItemAsync(PmsUpsertOutletItemDto dto, CancellationToken cancellationToken = default)
        {
            var hotelId = await GetCurrentHotelIdAsync(cancellationToken);
            var entity = new OutletItem
            {
                HotelId = hotelId,
                OutletId = dto.OutletId,
                CategoryId = dto.CategoryId,
                ItemCode = dto.ItemCode?.Trim(),
                ItemName = dto.ItemName.Trim(),
                ItemNameAr = dto.ItemNameAr?.Trim(),
                Description = dto.Description?.Trim(),
                Price = dto.Price,
                Quantity = dto.Quantity,
                ImageUrl = dto.ImageUrl?.Trim(),
                IncludesTax = dto.IncludesTax,
                IsActive = dto.IsActive,
                CreatedBy = ResolveCreatedBy(),
                CreatedAt = KsaTime.Now
            };

            await _context.OutletItems.AddAsync(entity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            var list = await ListItemsAsync(entity.OutletId, null, cancellationToken);
            return list.First(i => i.ItemId == entity.ItemId);
        }

        public async Task<PmsOutletItemDto?> UpdateItemAsync(int itemId, PmsUpsertOutletItemDto dto, CancellationToken cancellationToken = default)
        {
            var hotelId = await GetCurrentHotelIdAsync(cancellationToken);
            var entity = await _context.OutletItems.FirstOrDefaultAsync(i => i.ItemId == itemId && i.HotelId == hotelId, cancellationToken);
            if (entity == null)
            {
                return null;
            }

            entity.OutletId = dto.OutletId;
            entity.CategoryId = dto.CategoryId;
            entity.ItemCode = dto.ItemCode?.Trim();
            entity.ItemName = dto.ItemName.Trim();
            entity.ItemNameAr = dto.ItemNameAr?.Trim();
            entity.Description = dto.Description?.Trim();
            entity.Price = dto.Price;
            entity.Quantity = dto.Quantity;
            entity.ImageUrl = dto.ImageUrl?.Trim();
            entity.IncludesTax = dto.IncludesTax;
            entity.IsActive = dto.IsActive;
            entity.UpdatedAt = KsaTime.Now;
            await _context.SaveChangesAsync(cancellationToken);
            var list = await ListItemsAsync(entity.OutletId, null, cancellationToken);
            return list.FirstOrDefault(i => i.ItemId == itemId);
        }

        public async Task<IReadOnlyList<PmsOutletTableDto>> ListTablesAsync(int? outletId, CancellationToken cancellationToken = default)
        {
            var hotelId = await GetCurrentHotelIdAsync(cancellationToken);
            var q = _context.OutletTables.AsNoTracking().Where(t => t.HotelId == hotelId);
            if (outletId.HasValue)
            {
                q = q.Where(t => t.OutletId == outletId.Value);
            }

            return await q
                .OrderBy(t => t.TableName)
                .Select(t => new PmsOutletTableDto
                {
                    TableId = t.TableId,
                    HotelId = t.HotelId,
                    OutletId = t.OutletId,
                    OutletName = t.Outlet.OutletName,
                    TableName = t.TableName,
                    TableNameAr = t.TableNameAr,
                    Description = t.Description,
                    Capacity = t.Capacity,
                    Status = t.Status,
                    IsActive = t.IsActive
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<PmsOutletTableDto> CreateTableAsync(PmsUpsertOutletTableDto dto, CancellationToken cancellationToken = default)
        {
            var hotelId = await GetCurrentHotelIdAsync(cancellationToken);
            var entity = new OutletTable
            {
                HotelId = hotelId,
                OutletId = dto.OutletId,
                TableName = dto.TableName.Trim(),
                TableNameAr = dto.TableNameAr?.Trim(),
                Description = dto.Description?.Trim(),
                Capacity = dto.Capacity,
                Status = string.IsNullOrWhiteSpace(dto.Status) ? "Available" : dto.Status.Trim(),
                IsActive = dto.IsActive,
                CreatedBy = ResolveCreatedBy(),
                CreatedAt = KsaTime.Now
            };

            await _context.OutletTables.AddAsync(entity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            var list = await ListTablesAsync(dto.OutletId, cancellationToken);
            return list.First(t => t.TableId == entity.TableId);
        }

        public async Task<PmsOutletTableDto?> UpdateTableAsync(int tableId, PmsUpsertOutletTableDto dto, CancellationToken cancellationToken = default)
        {
            var hotelId = await GetCurrentHotelIdAsync(cancellationToken);
            var entity = await _context.OutletTables.FirstOrDefaultAsync(t => t.TableId == tableId && t.HotelId == hotelId, cancellationToken);
            if (entity == null)
            {
                return null;
            }

            entity.OutletId = dto.OutletId;
            entity.TableName = dto.TableName.Trim();
            entity.TableNameAr = dto.TableNameAr?.Trim();
            entity.Description = dto.Description?.Trim();
            entity.Capacity = dto.Capacity;
            entity.Status = string.IsNullOrWhiteSpace(dto.Status) ? entity.Status : dto.Status.Trim();
            entity.IsActive = dto.IsActive;
            entity.UpdatedAt = KsaTime.Now;
            await _context.SaveChangesAsync(cancellationToken);
            var list = await ListTablesAsync(dto.OutletId, cancellationToken);
            return list.FirstOrDefault(t => t.TableId == tableId);
        }

        public async Task<PmsPosCatalogDto?> GetPosCatalogAsync(int outletId, CancellationToken cancellationToken = default)
        {
            var outlet = await GetOutletAsync(outletId, cancellationToken);
            if (outlet == null || !outlet.IsActive)
            {
                return null;
            }

            var categories = (await ListCategoriesAsync(cancellationToken))
                .Where(c => c.IsActive)
                .ToList();
            var items = (await ListItemsAsync(outletId, null, cancellationToken))
                .Where(i => i.IsActive && (i.OutletId == null || i.OutletId == outletId))
                .ToList();
            var tables = (await ListTablesAsync(outletId, cancellationToken))
                .Where(t => t.IsActive)
                .ToList();

            var taxConfig = await HotelPricingTaxHelper.GetPosConfigAsync(_context, outlet.HotelId, cancellationToken);

            return new PmsPosCatalogDto
            {
                Outlet = outlet,
                Categories = categories,
                Items = items,
                Tables = tables,
                PricingTax = new PmsPosPricingTaxDto
                {
                    VatRate = taxConfig.VatRate,
                    EwaRate = taxConfig.EwaRate,
                    VatTaxIncluded = taxConfig.VatIncluded,
                    LodgingTaxIncluded = taxConfig.EwaIncluded
                }
            };
        }
    }
}
