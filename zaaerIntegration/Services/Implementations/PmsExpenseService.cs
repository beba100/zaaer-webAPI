#pragma warning disable CS1591

using FinanceLedgerAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Expense;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Models;
using zaaerIntegration.Services.Expense;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;
using ExpenseModel = FinanceLedgerAPI.Models.Expense;

namespace zaaerIntegration.Services.Implementations
{
    /// <summary>
    /// PMS façade over <see cref="ExpenseService"/> with tenant categories, VAT split, tenant expense_companies, and images.
    /// Child rows (expense_companies, expense_images) link via <c>expense_id</c> + legacy <c>old_expense_id</c> from the parent expense.
    /// VoM journal entries still resolve Master DB categories — tenant-only category ids may fail VoM until mapped.
    /// </summary>
    public sealed class PmsExpenseService : IPmsExpenseService
    {
        private readonly IExpenseService _expenseService;
        private readonly IExpenseImageService _expenseImageService;
        private readonly ApplicationDbContext _context;
        private readonly ITenantService _tenantService;
        private readonly IPmsCashLedgerService _cashLedger;

        public PmsExpenseService(
            IExpenseService expenseService,
            IExpenseImageService expenseImageService,
            ApplicationDbContext context,
            ITenantService tenantService,
            IPmsCashLedgerService cashLedger)
        {
            _expenseService = expenseService;
            _expenseImageService = expenseImageService;
            _context = context;
            _tenantService = tenantService;
            _cashLedger = cashLedger;
        }

        public async Task<PmsExpenseListResultDto> ListAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hotelContext = await ResolveHotelContextAsync(cancellationToken);
            var items = await _expenseService.GetAllAsync();
            var categories = await LoadTenantCategoriesAsync(hotelContext, cancellationToken);

            var rows = items
                .Where(e => MatchesDateFilter(e.DateTime, fromDate, toDate))
                .Select(item => MapToRow(item, categories))
                .OrderByDescending(r => r.DateTime)
                .ToList();

            var expenseIds = rows.Select(r => r.ExpenseId).ToList();
            var imageMap = await LoadFirstImageMapAsync(expenseIds, cancellationToken);
            foreach (var row in rows)
            {
                if (imageMap.TryGetValue(row.ExpenseId, out var img))
                {
                    row.FirstImageUrl = img.ImagePath;
                    row.ImageCount = img.Count;
                }
            }

            return new PmsExpenseListResultDto
            {
                Items = rows,
                Summary = BuildSummary(rows)
            };
        }

        public async Task<PmsExpenseDetailDto?> GetByIdAsync(long expenseId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hotelContext = await ResolveHotelContextAsync(cancellationToken);
            var item = await _expenseService.GetByIdAsync(expenseId);
            if (item == null)
            {
                return null;
            }

            var categories = await LoadTenantCategoriesAsync(hotelContext, cancellationToken);
            var detail = MapToDetail(item, categories);
            detail.Company = await LoadCompanyDtoAsync(expenseId, cancellationToken);
            detail.Images = (await _expenseImageService.GetImagesAsync(expenseId, hotelContext.HotelZaaerId, cancellationToken)).ToList();
            return detail;
        }

        public async Task<PmsExpenseDetailDto> CreateAsync(PmsCreateExpenseDto dto, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hotelContext = await ResolveHotelContextAsync(cancellationToken);
            ValidateCompany(dto.HasTax, dto.Company);

            var taxFields = await ComputeTaxFieldsAsync(dto.HasTax, dto.TotalAmount, cancellationToken);
            var createDto = new CreateExpenseDto
            {
                DateTime = dto.DateTime,
                DueDate = dto.DueDate,
                Comment = dto.Comment,
                ExpenseCategoryId = dto.ExpenseCategoryId,
                TaxRate = taxFields.TaxRate,
                TaxAmount = taxFields.TaxAmount,
                BeforeTaxAmount = taxFields.BeforeTaxAmount,
                TotalAmount = taxFields.TotalAmount,
                ExpenseRooms = dto.ExpenseRooms,
                PaymentSource = dto.PaymentSource,
                UseTenantCategories = true
            };

            var created = await _expenseService.CreateAsync(createDto);
            await UpsertCompanyAsync(created.ExpenseId, hotelContext.HotelZaaerId, dto.HasTax, dto.Company, cancellationToken);
            var createdExpense = await LoadExpenseEntityAsync(created.ExpenseId, cancellationToken);
            if (createdExpense != null)
            {
                await _cashLedger.SyncExpenseAsync(createdExpense, cancellationToken);
            }

            return (await GetByIdAsync(created.ExpenseId, cancellationToken))!;
        }

        public async Task<PmsExpenseDetailDto?> UpdateAsync(
            long expenseId,
            PmsUpdateExpenseDto dto,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hotelContext = await ResolveHotelContextAsync(cancellationToken);
            var existing = await _expenseService.GetByIdAsync(expenseId);
            if (existing == null)
            {
                return null;
            }

            var hasTax = dto.HasTax ?? (existing.TaxAmount.HasValue && existing.TaxAmount.Value > 0);
            if (dto.HasTax.HasValue || dto.Company != null)
            {
                ValidateCompany(hasTax, dto.Company);
            }

            var totalAmount = dto.TotalAmount ?? existing.TotalAmount;
            var taxFields = await ComputeTaxFieldsAsync(hasTax, totalAmount, cancellationToken);

            var updateDto = new UpdateExpenseDto
            {
                DateTime = dto.DateTime,
                DueDate = dto.DueDate,
                Comment = dto.Comment,
                ExpenseCategoryId = dto.ExpenseCategoryId,
                TaxRate = taxFields.TaxRate,
                TaxAmount = taxFields.TaxAmount,
                BeforeTaxAmount = taxFields.BeforeTaxAmount,
                TotalAmount = taxFields.TotalAmount,
                ExpenseRooms = dto.ExpenseRooms,
                ApprovalStatus = dto.ApprovalStatus,
                PaymentSource = dto.PaymentSource,
                UseTenantCategories = true
            };

            var updated = await _expenseService.UpdateAsync(expenseId, updateDto);
            if (updated == null)
            {
                return null;
            }

            await UpsertCompanyAsync(expenseId, hotelContext.HotelZaaerId, hasTax, dto.Company, cancellationToken);
            var updatedExpense = await LoadExpenseEntityAsync(expenseId, cancellationToken);
            if (updatedExpense != null)
            {
                await _cashLedger.SyncExpenseAsync(updatedExpense, cancellationToken);
            }
            return await GetByIdAsync(expenseId, cancellationToken);
        }

        public async Task<bool> DeleteAsync(long expenseId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var expense = await LoadExpenseEntityAsync(expenseId, cancellationToken);
            if (expense != null)
            {
                await _cashLedger.RemoveExpenseEffectAsync(expense, cancellationToken);
            }
            return await _expenseService.DeleteAsync(expenseId);
        }

        public async Task<PmsExpenseDetailDto?> ApproveAsync(
            long expenseId,
            PmsApproveExpenseRequestDto dto,
            int approvedByUserId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await _expenseService.ApproveExpenseAsync(
                expenseId,
                dto.Status,
                approvedByUserId,
                dto.RejectionReason,
                dto.Recommendation,
                dto.RecommendationToUserId);
            var expense = await LoadExpenseEntityAsync(expenseId, cancellationToken);
            if (expense != null)
            {
                await _cashLedger.SyncExpenseAsync(expense, cancellationToken);
            }
            return result == null ? null : await GetByIdAsync(expenseId, cancellationToken);
        }

        public async Task<IReadOnlyList<ExpenseApprovalHistoryDto>> GetApprovalHistoryAsync(
            long expenseId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var history = await _expenseService.GetApprovalHistoryAsync(expenseId);
            return history.ToList();
        }

        public async Task<IReadOnlyList<PmsExpenseCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hotelContext = await ResolveHotelContextAsync(cancellationToken);
            var categories = await LoadTenantCategoriesAsync(hotelContext, cancellationToken);
            return categories.Values
                .OrderBy(c => c.CategoryName)
                .Select(c => new PmsExpenseCategoryDto
                {
                    ExpenseCategoryId = c.ExpenseCategoryId,
                    CategoryName = c.CategoryName
                })
                .ToList();
        }

        public async Task<IReadOnlyList<PmsMasterCompanyRowDto>> SearchCompaniesAsync(
            string? search,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var term = (search ?? string.Empty).Trim();
            return await LoadDistinctTenantCompaniesAsync(term, cancellationToken);
        }

        public async Task<PmsMasterCompanyRowDto?> LookupCompanyByTaxNumberAsync(
            string taxNumber,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = NormalizeTaxNumber(taxNumber);
            if (string.IsNullOrEmpty(normalized))
            {
                return null;
            }

            var rows = await TenantExpenseCompaniesQuery()
                .OrderByDescending(ec => ec.Id)
                .Select(ec => new PmsMasterCompanyRowDto
                {
                    Id = ec.Id,
                    TaxNumber = ec.TaxNumber,
                    CompanyName = ec.CompanyName!
                })
                .ToListAsync(cancellationToken);

            return rows.FirstOrDefault(r =>
                string.Equals(NormalizeTaxNumber(r.TaxNumber), normalized, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeTaxNumber(string? value) => (value ?? string.Empty).Trim();

        private IQueryable<ExpenseCompany> TenantExpenseCompaniesQuery()
        {
            // Tenant DB is already hotel-scoped; include legacy rows with null/zero hotel_id.
            return _context.ExpenseCompanies
                .AsNoTracking()
                .Where(ec => ec.TaxNumber != null && ec.TaxNumber != "")
                .Where(ec => ec.CompanyName != null && ec.CompanyName != "");
        }

        private async Task<List<PmsMasterCompanyRowDto>> LoadDistinctTenantCompaniesAsync(
            string? term,
            CancellationToken cancellationToken)
        {
            var query = TenantExpenseCompaniesQuery();
            if (!string.IsNullOrEmpty(term))
            {
                query = query.Where(ec =>
                    ec.TaxNumber!.Contains(term) ||
                    ec.CompanyName!.Contains(term));
            }

            var rows = await query
                .OrderByDescending(ec => ec.Id)
                .Select(ec => new PmsMasterCompanyRowDto
                {
                    Id = ec.Id,
                    TaxNumber = ec.TaxNumber,
                    CompanyName = ec.CompanyName!
                })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(r => NormalizeTaxNumber(r.TaxNumber), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(r => r.CompanyName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<PmsExpenseTaxConfigDto> GetTaxConfigAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hotelContext = await ResolveHotelContextAsync(cancellationToken);
            var cfg = await HotelPricingTaxHelper.GetPosConfigAsync(_context, hotelContext.HotelZaaerId, cancellationToken);
            return new PmsExpenseTaxConfigDto
            {
                VatRate = cfg.VatRate,
                VatTaxIncluded = cfg.VatIncluded
            };
        }

        public async Task<IReadOnlyList<PmsExpenseImageDto>> GetImagesAsync(long expenseId, CancellationToken cancellationToken = default)
        {
            var hotelContext = await ResolveHotelContextAsync(cancellationToken);
            return await _expenseImageService.GetImagesAsync(expenseId, hotelContext.HotelZaaerId, cancellationToken);
        }

        public async Task<IReadOnlyList<PmsExpenseImageDto>> UploadImagesAsync(
            long expenseId,
            IReadOnlyList<IFormFile> images,
            CancellationToken cancellationToken = default)
        {
            var hotelContext = await ResolveHotelContextAsync(cancellationToken);
            return await _expenseImageService.UploadImagesAsync(expenseId, hotelContext.HotelZaaerId, images, cancellationToken);
        }

        public async Task<bool> DeleteImageAsync(long expenseId, int imageId, CancellationToken cancellationToken = default)
        {
            var hotelContext = await ResolveHotelContextAsync(cancellationToken);
            return await _expenseImageService.DeleteImageAsync(expenseId, imageId, hotelContext.HotelZaaerId, cancellationToken);
        }

        private sealed record HotelContext(int LocalHotelId, int HotelZaaerId);

        private sealed record TaxFields(decimal? TaxRate, decimal? TaxAmount, decimal BeforeTaxAmount, decimal TotalAmount);

        private Task<ExpenseModel?> LoadExpenseEntityAsync(long expenseId, CancellationToken cancellationToken) =>
            _context.Expenses
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.ExpenseId == expenseId, cancellationToken);

        private async Task<HotelContext> ResolveHotelContextAsync(CancellationToken cancellationToken)
        {
            var tenant = _tenantService.GetTenant()
                ?? throw new UnauthorizedAccessException("Tenant not resolved. Provide X-Hotel-Code.");

            var hotelSettings = await _context.HotelSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    h => h.HotelCode != null && h.HotelCode.ToLower() == tenant.Code.ToLower(),
                    cancellationToken)
                ?? throw new InvalidOperationException($"HotelSettings not found for hotel code: {tenant.Code}.");

            if (!hotelSettings.ZaaerId.HasValue)
            {
                throw new InvalidOperationException($"ZaaerId is not configured for hotel code: {tenant.Code}.");
            }

            return new HotelContext(hotelSettings.HotelId, hotelSettings.ZaaerId.Value);
        }

        /// <summary>
        /// expense_categories.hotel_id may match hotel_settings.hotel_id (PK) or zaaer_id (legacy / expenses convention).
        /// </summary>
        private async Task<List<int>> ResolveCategoryHotelIdsAsync(
            HotelContext hotelContext,
            CancellationToken cancellationToken)
        {
            var ids = new HashSet<int> { hotelContext.LocalHotelId, hotelContext.HotelZaaerId };

            var tenant = _tenantService.GetTenant();
            if (tenant != null)
            {
                var settings = await _context.HotelSettings
                    .AsNoTracking()
                    .Where(h => h.HotelCode != null && h.HotelCode.ToLower() == tenant.Code.ToLower())
                    .Select(h => new { h.HotelId, h.ZaaerId })
                    .ToListAsync(cancellationToken);

                foreach (var row in settings)
                {
                    ids.Add(row.HotelId);
                    if (row.ZaaerId.HasValue)
                    {
                        ids.Add(row.ZaaerId.Value);
                    }
                }
            }

            return ids.ToList();
        }

        private async Task<Dictionary<int, ExpenseCategory>> LoadTenantCategoriesAsync(
            HotelContext hotelContext,
            CancellationToken cancellationToken)
        {
            var hotelIds = await ResolveCategoryHotelIdsAsync(hotelContext, cancellationToken);

            var list = await _context.ExpenseCategories
                .AsNoTracking()
                .Where(c => hotelIds.Contains(c.HotelId) && c.IsActive)
                .ToListAsync(cancellationToken);

            // Tenant DB is hotel-scoped; if hotel_id values drifted, still return active categories.
            if (list.Count == 0)
            {
                list = await _context.ExpenseCategories
                    .AsNoTracking()
                    .Where(c => c.IsActive)
                    .ToListAsync(cancellationToken);
            }

            return list.ToDictionary(c => c.ExpenseCategoryId);
        }

        private async Task<TaxFields> ComputeTaxFieldsAsync(
            bool hasTax,
            decimal totalAmount,
            CancellationToken cancellationToken)
        {
            if (!hasTax || totalAmount <= 0)
            {
                return new TaxFields(null, null, totalAmount, totalAmount);
            }

            var hotelContext = await ResolveHotelContextAsync(cancellationToken);
            var cfg = await HotelPricingTaxHelper.GetPosConfigAsync(_context, hotelContext.HotelZaaerId, cancellationToken);
            var (net, _, vat, total) = HotelPricingTaxHelper.CalculateAmounts(totalAmount, cfg);
            return new TaxFields(cfg.VatRate, vat, net, total);
        }

        private static void ValidateCompany(bool hasTax, PmsExpenseCompanyDto? company)
        {
            if (!hasTax)
            {
                return;
            }

            if (company == null ||
                string.IsNullOrWhiteSpace(company.TaxNumber) ||
                string.IsNullOrWhiteSpace(company.CompanyName))
            {
                throw new ArgumentException("Tax number and company name are required for taxable expenses.");
            }
        }

        private async Task UpsertCompanyAsync(
            long expenseId,
            int hotelZaaerId,
            bool hasTax,
            PmsExpenseCompanyDto? company,
            CancellationToken cancellationToken)
        {
            var existing = await _context.ExpenseCompanies
                .FirstOrDefaultAsync(c => c.ExpenseId == expenseId, cancellationToken);

            if (!hasTax)
            {
                if (existing != null)
                {
                    _context.ExpenseCompanies.Remove(existing);
                    await _context.SaveChangesAsync(cancellationToken);
                }

                return;
            }

            ValidateCompany(true, company);

            var oldExpenseId = await ResolveExpenseOldExpenseIdAsync(expenseId, cancellationToken);

            if (existing == null)
            {
                existing = new ExpenseCompany
                {
                    ExpenseId = expenseId,
                    OldExpenseId = oldExpenseId,
                    HotelId = hotelZaaerId,
                    CreatedAt = KsaTime.Now
                };
                await _context.ExpenseCompanies.AddAsync(existing, cancellationToken);
            }

            existing.ExpenseId = expenseId;
            existing.OldExpenseId = oldExpenseId;
            existing.TaxNumber = company!.TaxNumber!.Trim();
            existing.CompanyName = company.CompanyName!.Trim();
            existing.CompanyId = null;
            existing.HotelId = hotelZaaerId;
            existing.UpdatedAt = KsaTime.Now;
            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<int> ResolveExpenseOldExpenseIdAsync(long expenseId, CancellationToken cancellationToken)
        {
            var oldExpenseId = await _context.Expenses
                .AsNoTracking()
                .Where(e => e.ExpenseId == expenseId)
                .Select(e => e.OldExpenseId)
                .FirstOrDefaultAsync(cancellationToken);

            if (oldExpenseId <= 0)
            {
                throw new InvalidOperationException(
                    $"Expense {expenseId} has no old_expense_id — cannot insert expense_companies row.");
            }

            return oldExpenseId;
        }

        private sealed record ExpenseImageSummary(string ImagePath, int Count);

        private async Task<Dictionary<long, ExpenseImageSummary>> LoadFirstImageMapAsync(
            IReadOnlyList<long> expenseIds,
            CancellationToken cancellationToken)
        {
            if (expenseIds.Count == 0)
            {
                return new Dictionary<long, ExpenseImageSummary>();
            }

            var images = await _context.ExpenseImages
                .AsNoTracking()
                .Where(i => expenseIds.Contains(i.ExpenseId))
                .OrderBy(i => i.ExpenseId)
                .ThenBy(i => i.DisplayOrder)
                .ThenBy(i => i.ExpenseImageId)
                .ToListAsync(cancellationToken);

            return images
                .GroupBy(i => i.ExpenseId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var first = g.First();
                        return new ExpenseImageSummary(first.ImagePath, g.Count());
                    });
        }

        private static bool MatchesDateFilter(DateTime dateTime, DateTime? fromDate, DateTime? toDate)
        {
            var day = dateTime.Date;
            if (fromDate.HasValue && day < fromDate.Value.Date)
            {
                return false;
            }

            if (toDate.HasValue && day > toDate.Value.Date)
            {
                return false;
            }

            return true;
        }

        private static PmsExpenseSummaryDto BuildSummary(IReadOnlyList<PmsExpenseRowDto> rows)
        {
            decimal total = 0m;
            decimal before = 0m;
            decimal tax = 0m;

            foreach (var row in rows)
            {
                total += row.TotalAmount;
                before += row.BeforeTaxAmount ?? row.TotalAmount;
                tax += row.TaxAmount ?? 0m;
            }

            return new PmsExpenseSummaryDto
            {
                Count = rows.Count,
                TotalAmount = Math.Round(total, 2, MidpointRounding.AwayFromZero),
                BeforeTaxAmount = Math.Round(before, 2, MidpointRounding.AwayFromZero),
                TaxAmount = Math.Round(tax, 2, MidpointRounding.AwayFromZero)
            };
        }

        private static decimal ResolveBeforeTaxAmount(ExpenseResponseDto source)
        {
            if (source.BeforeTaxAmount.HasValue)
            {
                return source.BeforeTaxAmount.Value;
            }

            if (source.TaxAmount.HasValue && source.TaxAmount.Value > 0)
            {
                return source.TotalAmount - source.TaxAmount.Value;
            }

            return source.TotalAmount;
        }

        private static string ResolveExpenseNo(ExpenseResponseDto source)
        {
            if (!string.IsNullOrWhiteSpace(source.ExpenseNo))
            {
                return source.ExpenseNo.Trim();
            }

            if (source.ExpenseSeq > 0)
            {
                return $"EXP_{source.ExpenseSeq:D4}";
            }

            return source.ExpenseId > 0 ? $"EXP-{source.ExpenseId}" : string.Empty;
        }

        private async Task<PmsExpenseCompanyDto?> LoadCompanyDtoAsync(long expenseId, CancellationToken cancellationToken)
        {
            var company = await _context.ExpenseCompanies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ExpenseId == expenseId, cancellationToken);

            if (company == null)
            {
                return null;
            }

            return new PmsExpenseCompanyDto
            {
                Id = company.Id,
                TaxNumber = company.TaxNumber,
                CompanyName = company.CompanyName,
                CompanyId = company.CompanyId
            };
        }

        private static PmsExpenseRowDto MapToRow(
            ExpenseResponseDto source,
            IReadOnlyDictionary<int, ExpenseCategory> tenantCategories)
        {
            var categoryName = source.ExpenseCategoryId.HasValue &&
                               tenantCategories.TryGetValue(source.ExpenseCategoryId.Value, out var category)
                ? category.CategoryName
                : source.ExpenseCategoryName;

            return new PmsExpenseRowDto
            {
                ExpenseId = source.ExpenseId,
                ExpenseNo = ResolveExpenseNo(source),
                ExpenseSeq = source.ExpenseSeq,
                DateTime = source.DateTime,
                DueDate = source.DueDate,
                Comment = source.Comment,
                ExpenseCategoryId = source.ExpenseCategoryId,
                ExpenseCategoryName = categoryName,
                TaxRate = source.TaxRate,
                TaxAmount = source.TaxAmount,
                BeforeTaxAmount = ResolveBeforeTaxAmount(source),
                TotalAmount = source.TotalAmount,
                ApprovalStatus = source.ApprovalStatus,
                PaymentSource = source.PaymentSource,
                HotelName = source.HotelName,
                CreatedAt = source.CreatedAt
            };
        }

        private static PmsExpenseDetailDto MapToDetail(
            ExpenseResponseDto source,
            IReadOnlyDictionary<int, ExpenseCategory> tenantCategories)
        {
            var row = MapToRow(source, tenantCategories);
            return new PmsExpenseDetailDto
            {
                ExpenseId = row.ExpenseId,
                ExpenseNo = row.ExpenseNo,
                ExpenseSeq = row.ExpenseSeq,
                DateTime = row.DateTime,
                DueDate = row.DueDate,
                Comment = row.Comment,
                ExpenseCategoryId = row.ExpenseCategoryId,
                ExpenseCategoryName = row.ExpenseCategoryName,
                TaxRate = row.TaxRate,
                TaxAmount = row.TaxAmount,
                BeforeTaxAmount = row.BeforeTaxAmount,
                TotalAmount = row.TotalAmount,
                ApprovalStatus = row.ApprovalStatus,
                PaymentSource = row.PaymentSource,
                HotelName = row.HotelName,
                CreatedAt = row.CreatedAt,
                ApprovedBy = source.ApprovedBy,
                ApprovedByFullName = source.ApprovedByFullName,
                ApprovedAt = source.ApprovedAt,
                RejectionReason = source.RejectionReason,
                ExpenseRooms = source.ExpenseRooms ?? new List<ExpenseRoomResponseDto>()
            };
        }
    }
}
