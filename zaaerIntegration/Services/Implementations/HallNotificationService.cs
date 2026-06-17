namespace zaaerIntegration.Services.Implementations
{
    /// <summary>
    /// Stub notification service — integrate WhatsApp/SMS gateway when available.
    /// </summary>
    public sealed class HallNotificationService : Interfaces.IHallNotificationService
    {
        private readonly ILogger<HallNotificationService> _logger;

        public HallNotificationService(ILogger<HallNotificationService> logger)
        {
            _logger = logger;
        }

        public Task NotifyEventTomorrowAsync(int reservationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Hall notification hook: event tomorrow for reservation {ReservationId}", reservationId);
            return Task.CompletedTask;
        }

        public Task NotifyDepositDueAsync(int reservationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Hall notification hook: deposit due for reservation {ReservationId}", reservationId);
            return Task.CompletedTask;
        }

        public Task NotifyBalanceDueAsync(int reservationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Hall notification hook: balance due for reservation {ReservationId}", reservationId);
            return Task.CompletedTask;
        }

        public Task NotifyEventConfirmedAsync(int reservationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Hall notification hook: event confirmed for reservation {ReservationId}", reservationId);
            return Task.CompletedTask;
        }
    }
}
