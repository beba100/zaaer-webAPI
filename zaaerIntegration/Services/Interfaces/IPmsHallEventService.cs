using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Reporting.Abstractions;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IPmsHallEventService
    {
        Task<PmsHallEventLookupsDto> GetLookupsAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PmsHallEventListItemDto>> ListEventsAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? eventStatus = null,
            int? hallId = null,
            string? fromDateHijri = null,
            string? toDateHijri = null,
            string? eventDateHijri = null,
            int? hijriYear = null,
            int? hijriMonth = null,
            CancellationToken cancellationToken = default);
        Task<PmsHallEventDetailDto?> GetEventAsync(int reservationId, CancellationToken cancellationToken = default);
        Task<PmsHallEventDetailDto> CreateEventAsync(PmsCreateHallEventDto dto, CancellationToken cancellationToken = default);
        Task<PmsHallEventDetailDto?> UpdateEventScheduleAsync(
            int reservationId,
            PmsUpdateHallEventScheduleDto dto,
            CancellationToken cancellationToken = default);
        Task<PmsHallEventDetailDto?> UpdateEventAsync(
            int reservationId,
            PmsUpdateHallEventDto dto,
            CancellationToken cancellationToken = default);
        Task<PmsHallEventDetailDto?> TransitionStatusAsync(
            int reservationId,
            PmsTransitionHallEventStatusDto dto,
            CancellationToken cancellationToken = default);
        Task<PmsHallEventDetailDto?> CheckInEventAsync(
            int reservationId,
            CancellationToken cancellationToken = default);
        Task<PmsHallEventDetailDto?> RecordDepositAsync(
            int reservationId,
            PmsRecordHallDepositDto dto,
            CancellationToken cancellationToken = default);
        Task<PmsHallEventDetailDto?> CompleteEventAsync(
            int reservationId,
            PmsCompleteHallEventDto dto,
            CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PmsHallSchedulerItemDto>> GetSchedulerItemsAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default);
        Task<PmsHallDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PmsHallOccupancyCardDto>> GetOccupancyAsync(CancellationToken cancellationToken = default);
        Task SyncOperationalStatusesAsync(CancellationToken cancellationToken = default);
        Task<PmsFunctionSheetDto?> GetFunctionSheetAsync(int reservationId, CancellationToken cancellationToken = default);
        Task<PmsFunctionSheetDto> UpsertFunctionSheetAsync(int reservationId, PmsFunctionSheetDto dto, CancellationToken cancellationToken = default);
        Task<PmsFunctionSheetDto?> ApproveFunctionSheetAsync(int reservationId, CancellationToken cancellationToken = default);
        Task<ReportRenderResult?> PrintFunctionSheetAsync(int reservationId, CancellationToken cancellationToken = default);
        Task<ReportRenderResult?> PrintContractAsync(int reservationId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PmsHallEventAlertDto>> ListAlertsAsync(bool unreadOnly = false, CancellationToken cancellationToken = default);
        Task MarkAlertReadAsync(int alertId, CancellationToken cancellationToken = default);
        Task GenerateAlertsAsync(CancellationToken cancellationToken = default);
        Task<PmsHallDailyEventsReportDto> GetDailyEventsReportAsync(DateTime date, CancellationToken cancellationToken = default);
        Task<PmsHallUtilizationReportDto> GetUtilizationReportAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
        Task<PmsHallUnpaidBalancesPageDto> GetUnpaidBalancesAsync(
            int skip = 0,
            int take = 50,
            bool countOnly = false,
            CancellationToken cancellationToken = default);
        Task<PmsHallEventSettlementDto?> GetSettlementAsync(
            int reservationId,
            CancellationToken cancellationToken = default);
        Task<bool> UpdateHallPreparationAsync(int hallId, PmsUpdateHallPreparationDto dto, CancellationToken cancellationToken = default);
    }
}
