using zaaerIntegration.DTOs.Pms.ActivityLog;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IReservationActivityLogQueryService
    {
        Task<IReadOnlyList<PmsActivityLogItemDto>> ListForReservationAsync(
            int reservationRouteId,
            int? hotelId,
            int skip,
            int take,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PmsActivityLogItemDto>> SearchAsync(
            PmsActivityLogQueryDto query,
            CancellationToken cancellationToken = default);
    }
}
