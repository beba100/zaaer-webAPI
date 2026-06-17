using FinanceLedgerAPI.Models;
using zaaerIntegration.DTOs.Pms;
using ExpenseModel = FinanceLedgerAPI.Models.Expense;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IPmsCashLedgerService
    {
        Task BackfillAsync(CancellationToken cancellationToken = default);

        Task SyncPaymentReceiptAsync(PaymentReceipt receipt, CancellationToken cancellationToken = default);

        Task SyncExpenseAsync(ExpenseModel expense, CancellationToken cancellationToken = default);

        Task RemoveExpenseEffectAsync(ExpenseModel expense, CancellationToken cancellationToken = default);

        Task<PmsCashLedgerStatementDto> GetReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default);
    }
}
