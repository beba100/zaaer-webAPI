#pragma warning disable CS1591

using zaaerIntegration.DTOs.Pms;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IPmsHallReportService
    {
        Task<PmsHallBookingsReportDto> GetBookingsReportAsync(
            DateTime fromDate,
            DateTime toDate,
            string? eventStatus = null,
            int? hallId = null,
            CancellationToken cancellationToken = default);

        Task<PmsHallFinanceReportDto> GetReceiptsReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default);

        Task<PmsHallFinanceReportDto> GetDisbursementsReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default);

        Task<PmsHallFinanceReportDto> GetInvoicesReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default);

        Task<PmsHallFinanceReportDto> GetCreditNotesReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default);

        Task<PmsDailyJournalReportDto> GetDailyJournalReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default);

        Task<PmsNetworkCashPaymentsReportDto> GetNetworkCashPaymentsReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default);
    }
}
