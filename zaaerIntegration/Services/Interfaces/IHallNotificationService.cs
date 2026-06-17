namespace zaaerIntegration.Services.Interfaces
{
    /// <summary>
    /// Notification hooks for hall events (WhatsApp/SMS integration points).
    /// </summary>
    public interface IHallNotificationService
    {
        Task NotifyEventTomorrowAsync(int reservationId, CancellationToken cancellationToken = default);
        Task NotifyDepositDueAsync(int reservationId, CancellationToken cancellationToken = default);
        Task NotifyBalanceDueAsync(int reservationId, CancellationToken cancellationToken = default);
        Task NotifyEventConfirmedAsync(int reservationId, CancellationToken cancellationToken = default);
    }
}
