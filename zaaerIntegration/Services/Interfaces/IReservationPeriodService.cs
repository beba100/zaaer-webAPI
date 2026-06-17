#pragma warning disable CS1591

using zaaerIntegration.DTOs.Pms.ReservationDetail;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IReservationPeriodService
    {
        Task<ReservationPeriodListResponseDto?> GetPeriodsAsync(
            int routeId,
            int? hotelId,
            CancellationToken cancellationToken = default);

        Task<ReservationPeriodListResponseDto?> CreateInitialPeriodAsync(
            int routeId,
            int? hotelId,
            CancellationToken cancellationToken = default);

        Task<ReservationPeriodAppendResultDto?> AppendPeriodAsync(
            int routeId,
            ReservationPeriodAppendRequestDto request,
            int? hotelId,
            CancellationToken cancellationToken = default);

        Task<ReservationPeriodAppendResultDto?> UpdateActivePeriodAsync(
            int routeId,
            int periodId,
            ReservationPeriodUpdateRequestDto request,
            int? hotelId,
            CancellationToken cancellationToken = default);
    }
}
