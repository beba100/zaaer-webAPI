using zaaerIntegration.DTOs.Pms;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IPmsHotelTargetService
    {
        Task<PmsHotelTargetReportDto> GetTargetReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PmsHotelMonthlyTargetDto>> ListTargetsForCurrentHotelAsync(
            CancellationToken cancellationToken = default);

        Task<PmsHotelMonthlyTargetDto> CreateTargetAsync(
            UpsertPmsHotelMonthlyTargetDto dto,
            CancellationToken cancellationToken = default);

        Task<PmsHotelMonthlyTargetDto> UpdateTargetAsync(
            int id,
            UpsertPmsHotelMonthlyTargetDto dto,
            CancellationToken cancellationToken = default);
    }
}
