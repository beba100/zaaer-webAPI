using Microsoft.EntityFrameworkCore;
using FinanceLedgerAPI.Models;
using zaaerIntegration.Data;
using zaaerIntegration.Repositories.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Repositories.Implementations
{
    /// <summary>
    /// Customer Repository Implementation
    /// تنفيذ مستودع العملاء
    /// </summary>
    public class CustomerRepository : GenericRepository<Customer>, ICustomerRepository
    {
        public CustomerRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Customer?> GetByCustomerNoAsync(string customerNo)
        {
            return await _dbSet
                .Include(c => c.GuestType)
                .Include(c => c.Nationality)
                .Include(c => c.GuestCategory)
                .FirstOrDefaultAsync(c => c.CustomerNo == customerNo);
        }

        public async Task<IEnumerable<Customer>> GetByHotelIdAsync(int hotelId)
        {
            return await _dbSet
                .Include(c => c.GuestType)
                .Include(c => c.Nationality)
                .Include(c => c.GuestCategory)
                .Where(c => c.EnteredBy == hotelId) // Assuming EnteredBy represents hotel context
                .ToListAsync();
        }

        public async Task<IEnumerable<Customer>> SearchByNameAsync(string name)
        {
            return await PmsCustomerMarkers.ExcludeDraftPlaceholders(_dbSet)
                .Include(c => c.GuestType)
                .Include(c => c.Nationality)
                .Include(c => c.GuestCategory)
                .Where(c => c.CustomerName.Contains(name))
                .ToListAsync();
        }

        public async Task<IEnumerable<Customer>> GetWithRelatedDataAsync()
        {
            return await PmsCustomerMarkers.ExcludeDraftPlaceholders(_dbSet)
                .Include(c => c.GuestType)
                .Include(c => c.Nationality)
                .Include(c => c.GuestCategory)
                .Include(c => c.Identifications)
                .Include(c => c.CustomerAccounts)
                .ToListAsync();
        }

        public async Task<Customer?> GetWithRelatedDataByIdAsync(int customerId)
        {
            return await _dbSet
                .Include(c => c.GuestType)
                .Include(c => c.Nationality)
                .Include(c => c.GuestCategory)
                .Include(c => c.Identifications)
                .Include(c => c.CustomerAccounts)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);
        }

        public async Task<IEnumerable<Customer>> GetByNationalityAsync(int nationalityId)
        {
            return await PmsCustomerMarkers.ExcludeDraftPlaceholders(_dbSet)
                .Include(c => c.GuestType)
                .Include(c => c.Nationality)
                .Include(c => c.GuestCategory)
                .Where(c => c.NId == nationalityId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Customer>> GetByGuestTypeAsync(int guestTypeId)
        {
            return await PmsCustomerMarkers.ExcludeDraftPlaceholders(_dbSet)
                .Include(c => c.GuestType)
                .Include(c => c.Nationality)
                .Include(c => c.GuestCategory)
                .Where(c => c.GtypeId == guestTypeId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Customer>> GetByGuestCategoryAsync(int guestCategoryId)
        {
            return await PmsCustomerMarkers.ExcludeDraftPlaceholders(_dbSet)
                .Include(c => c.GuestType)
                .Include(c => c.Nationality)
                .Include(c => c.GuestCategory)
                .Where(c => c.GuestCategoryId == guestCategoryId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Customer>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate)
        {
            return await PmsCustomerMarkers.ExcludeDraftPlaceholders(_dbSet)
                .Include(c => c.GuestType)
                .Include(c => c.Nationality)
                .Include(c => c.GuestCategory)
                .Where(c => c.EnteredAt >= fromDate && c.EnteredAt <= toDate)
                .ToListAsync();
        }

        public async Task<object> GetCustomerStatisticsAsync()
        {
            var guestQuery = PmsCustomerMarkers.ExcludeDraftPlaceholders(_dbSet);
            var totalCustomers = await guestQuery.CountAsync();
            var customersByGender = await guestQuery
                .GroupBy(c => c.Gender)
                .Select(g => new { Gender = g.Key, Count = g.Count() })
                .ToListAsync();
            
            var customersByNationality = await guestQuery
                .Include(c => c.Nationality)
                .GroupBy(c => c.Nationality != null ? c.Nationality.NName : "Unknown")
                .Select(g => new { Nationality = g.Key, Count = g.Count() })
                .ToListAsync();

            var recentCustomers = await guestQuery
                .Where(c => c.EnteredAt >= DateTime.Now.AddDays(-30))
                .CountAsync();

            return new
            {
                TotalCustomers = totalCustomers,
                RecentCustomers = recentCustomers,
                CustomersByGender = customersByGender,
                CustomersByNationality = customersByNationality
            };
        }

        public async Task<(IEnumerable<Customer> Customers, int TotalCount)> GetPagedWithFiltersAsync(
            int pageNumber,
            int pageSize,
            string? searchTerm,
            string? searchMode,
            int? nationalityId,
            int? guestCategoryId)
        {
            // Identifications navigation is Ignored on Customer in DbContext; batch-load after paging.
            IQueryable<Customer> query = PmsCustomerMarkers.ExcludeDraftPlaceholders(_dbSet)
                .AsNoTracking()
                .Include(c => c.GuestType)
                .Include(c => c.Nationality)
                .Include(c => c.GuestCategory);

            if (nationalityId.HasValue)
            {
                query = query.Where(c => c.NId == nationalityId.Value);
            }

            if (guestCategoryId.HasValue)
            {
                query = query.Where(c => c.GuestCategoryId == guestCategoryId.Value);
            }

            var mode = (searchMode ?? "name").Trim().ToLowerInvariant();
            var term = searchTerm?.Trim();

            if (!string.IsNullOrEmpty(term))
            {
                switch (mode)
                {
                    case "mobile":
                        query = query.Where(c => c.MobileNo != null && c.MobileNo.Contains(term));
                        break;
                    case "id":
                        // customer_identifications.customer_id stores customers.zaaer_id (integration), not always PK.
                        var identCustKeys = _context.Set<CustomerIdentification>()
                            .AsNoTracking()
                            .Where(i => i.IdNumber.Contains(term))
                            .Select(i => i.CustomerId);

                        if (int.TryParse(term, out var idNum))
                        {
                            query = query.Where(c =>
                                c.CustomerId == idNum ||
                                (c.ZaaerId.HasValue && c.ZaaerId.Value == idNum) ||
                                (c.CustomerNo != null && c.CustomerNo.Contains(term)) ||
                                identCustKeys.Contains(c.CustomerId) ||
                                (c.ZaaerId.HasValue && identCustKeys.Contains(c.ZaaerId.Value)));
                        }
                        else
                        {
                            query = query.Where(c =>
                                (c.CustomerNo != null && c.CustomerNo.Contains(term)) ||
                                identCustKeys.Contains(c.CustomerId) ||
                                (c.ZaaerId.HasValue && identCustKeys.Contains(c.ZaaerId.Value)));
                        }

                        break;
                    default:
                        query = query.Where(c => c.CustomerName.Contains(term));
                        break;
                }
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(c => c.CustomerName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            if (items.Count > 0)
            {
                // Match rows where ident.customer_id = customer.ZaaerId OR ident.customer_id = customer.CustomerId (legacy).
                var keys = items
                    .SelectMany(c => new[] { c.CustomerId }.Concat(c.ZaaerId.HasValue ? new[] { c.ZaaerId.Value } : Array.Empty<int>()))
                    .Distinct()
                    .ToList();

                var identRows = await _context.Set<CustomerIdentification>()
                    .AsNoTracking()
                    .Where(i => keys.Contains(i.CustomerId))
                    .ToListAsync();

                var idTypeIds = identRows.Select(i => i.IdTypeId).Distinct().ToList();
                var idTypes = await _context.Set<IdType>()
                    .AsNoTracking()
                    .Where(t =>
                        idTypeIds.Contains(t.ItId) ||
                        (t.ZaaerId.HasValue && idTypeIds.Contains(t.ZaaerId.Value)))
                    .ToListAsync();

                var idTypeByAnyKey = new Dictionary<int, IdType>();
                foreach (var t in idTypes)
                {
                    idTypeByAnyKey[t.ItId] = t;
                    if (t.ZaaerId.HasValue)
                    {
                        idTypeByAnyKey[t.ZaaerId.Value] = t;
                    }
                }

                foreach (var i in identRows)
                {
                    if (idTypeByAnyKey.TryGetValue(i.IdTypeId, out var idt))
                    {
                        i.IdType = idt;
                    }
                }

                foreach (var c in items)
                {
                    var list = identRows
                        .Where(i =>
                            i.CustomerId == c.CustomerId ||
                            (c.ZaaerId.HasValue && i.CustomerId == c.ZaaerId.Value))
                        .ToList();
                    c.Identifications = list;
                }
            }

            return (items, totalCount);
        }
    }
}
