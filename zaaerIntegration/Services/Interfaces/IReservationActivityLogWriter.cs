using zaaerIntegration.Services.ActivityLog;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IReservationActivityLogWriter
    {
        Task LogAsync(ReservationActivityLogEntry entry, CancellationToken cancellationToken = default);
    }
}
