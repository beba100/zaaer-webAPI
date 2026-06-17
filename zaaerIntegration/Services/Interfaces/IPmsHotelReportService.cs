#pragma warning disable CS1591

using zaaerIntegration.DTOs.Pms;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IPmsHotelReportService
    {
        Task<PmsHotelBookingsReportDto> GetBookingsReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default);

        Task<PmsHotelFinanceReportDto> GetReceiptsReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default);

        Task<PmsHotelFinanceReportDto> GetDisbursementsReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default);

        Task<PmsHotelFinanceReportDto> GetInvoicesReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default);

        Task<PmsHotelFinanceReportDto> GetCreditNotesReportAsync(
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

        Task<PmsHotelDeparturesReportDto> GetDeparturesReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default);

        Task<PmsHotelOnlineBookingsReportDto> GetOnlineBookingsReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default);

        Task<PmsHotelUnitTransfersReportDto> GetUnitTransfersReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default);

        Task<PmsHotelMonthEndClosingReportDto> GetMonthEndClosingReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default);
    }
}
