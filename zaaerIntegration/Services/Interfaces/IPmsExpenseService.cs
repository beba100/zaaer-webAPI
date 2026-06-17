#pragma warning disable CS1591

using Microsoft.AspNetCore.Http;
using zaaerIntegration.DTOs.Expense;
using zaaerIntegration.DTOs.Pms;

namespace zaaerIntegration.Services.Interfaces
{
    /// <summary>
    /// PMS hotel-scoped expenses (wraps tenant <see cref="Expense.IExpenseService"/> + central numbering on create).
    /// </summary>
    public interface IPmsExpenseService
    {
        Task<PmsExpenseListResultDto> ListAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken cancellationToken = default);

        Task<PmsExpenseDetailDto?> GetByIdAsync(long expenseId, CancellationToken cancellationToken = default);

        Task<PmsExpenseDetailDto> CreateAsync(PmsCreateExpenseDto dto, CancellationToken cancellationToken = default);

        Task<PmsExpenseDetailDto?> UpdateAsync(long expenseId, PmsUpdateExpenseDto dto, CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(long expenseId, CancellationToken cancellationToken = default);

        Task<PmsExpenseDetailDto?> ApproveAsync(
            long expenseId,
            PmsApproveExpenseRequestDto dto,
            int approvedByUserId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ExpenseApprovalHistoryDto>> GetApprovalHistoryAsync(
            long expenseId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PmsExpenseCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);

        Task<PmsExpenseTaxConfigDto> GetTaxConfigAsync(CancellationToken cancellationToken = default);

        /// <summary>Distinct supplier companies from tenant <c>expense_companies</c> (not Master DB).</summary>
        Task<IReadOnlyList<PmsMasterCompanyRowDto>> SearchCompaniesAsync(
            string? search,
            CancellationToken cancellationToken = default);

        /// <summary>Exact tax-number lookup in tenant <c>expense_companies</c> (newest row wins).</summary>
        Task<PmsMasterCompanyRowDto?> LookupCompanyByTaxNumberAsync(
            string taxNumber,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PmsExpenseImageDto>> GetImagesAsync(long expenseId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PmsExpenseImageDto>> UploadImagesAsync(
            long expenseId,
            IReadOnlyList<IFormFile> images,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteImageAsync(long expenseId, int imageId, CancellationToken cancellationToken = default);
    }
}
