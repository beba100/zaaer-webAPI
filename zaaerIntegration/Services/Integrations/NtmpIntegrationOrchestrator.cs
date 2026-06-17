using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using zaaerIntegration.Data;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Integrations
{
    public interface INtmpIntegrationOrchestrator
    {
        Task SyncBookingAsync(int reservationId, NtmpBookingOperation operation, CancellationToken cancellationToken = default);
        Task SyncCancelAsync(int reservationId, CancellationToken cancellationToken = default);
        Task SyncExpenseAsync(int reservationId, CancellationToken cancellationToken = default);
        Task SyncOccupancyAsync(int hotelId, CancellationToken cancellationToken = default);
        Task RetryReservationAsync(int reservationId, CancellationToken cancellationToken = default);
    }

    public sealed class NtmpIntegrationOrchestrator : INtmpIntegrationOrchestrator
    {
        private readonly ApplicationDbContext _db;
        private readonly INtmpBookingPayloadBuilder _payloadBuilder;
        private readonly INtmpGatewayClient _gateway;
        private readonly INtmpPasswordResolver _passwordResolver;
        private readonly INtmpIntegrationSchemaEnsurer _schemaEnsurer;
        private readonly ILogger<NtmpIntegrationOrchestrator> _logger;

        public NtmpIntegrationOrchestrator(
            ApplicationDbContext db,
            INtmpBookingPayloadBuilder payloadBuilder,
            INtmpGatewayClient gateway,
            INtmpPasswordResolver passwordResolver,
            INtmpIntegrationSchemaEnsurer schemaEnsurer,
            ILogger<NtmpIntegrationOrchestrator> logger)
        {
            _db = db;
            _payloadBuilder = payloadBuilder;
            _gateway = gateway;
            _passwordResolver = passwordResolver;
            _schemaEnsurer = schemaEnsurer;
            _logger = logger;
        }

        public async Task SyncBookingAsync(
            int reservationId,
            NtmpBookingOperation operation,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _schemaEnsurer.EnsureAsync(cancellationToken);
                var reservation = await _db.Reservations
                    .FirstOrDefaultAsync(r => r.ReservationId == reservationId, cancellationToken);
                if (reservation == null)
                {
                    return;
                }

                var settings = await LoadActiveSettingsAsync(reservation.HotelId, cancellationToken);
                if (settings == null)
                {
                    return;
                }

                var passwordResult = _passwordResolver.Resolve(settings);
                if (!passwordResult.IsConfigured)
                {
                    var logEvent = string.IsNullOrWhiteSpace(reservation.NtmpTransactionId)
                        ? "CreateBooking"
                        : "UpdateBooking";
                    await LogAsync(
                        reservation,
                        logEvent,
                        null,
                        null,
                        "Error",
                        passwordResult.ErrorMessage,
                        null,
                        null,
                        null,
                        cancellationToken);
                    return;
                }

                var password = passwordResult.Password!;
                var plan = NtmpBookingSyncPlanner.PlanSync(reservation, operation);
                var built = await _payloadBuilder.BuildCreateOrUpdateAsync(
                    reservationId,
                    plan.TransactionTypeId,
                    plan.CuFlag,
                    plan.IncludeTransactionId,
                    cancellationToken);
                if (!built.Success || built.Request == null)
                {
                    await LogAsync(
                        reservation,
                        plan.LogEventType,
                        null,
                        null,
                        "Error",
                        built.ErrorMessage,
                        null,
                        null,
                        null,
                        cancellationToken);
                    return;
                }

                built.Request.Channel = settings.ChannelName ?? built.Request.Channel;
                built.Request.CuFlag = plan.CuFlag;
                built.Request.TransactionTypeId = plan.TransactionTypeId.ToString();
                if (!plan.IncludeTransactionId)
                {
                    built.Request.TransactionId = null;
                }
                else if (!string.IsNullOrWhiteSpace(reservation.NtmpTransactionId))
                {
                    built.Request.TransactionId = reservation.NtmpTransactionId;
                }

                var response = await _gateway.CreateOrUpdateBookingAsync(settings, password, built.Request, cancellationToken);
                if (!response.Success
                    && response.ErrorCodes.Contains("33")
                    && string.Equals(plan.CuFlag, "2", StringComparison.Ordinal))
                {
                    reservation.NtmpSyncedStages &= ~plan.StageBits;
                    built.Request.CuFlag = "1";
                    if (plan.TransactionTypeId == NtmpApiConstants.TransactionTypeBooking)
                    {
                        built.Request.TransactionId = null;
                    }
                    else if (!string.IsNullOrWhiteSpace(reservation.NtmpTransactionId))
                    {
                        built.Request.TransactionId = reservation.NtmpTransactionId;
                    }

                    response = await _gateway.CreateOrUpdateBookingAsync(settings, password, built.Request, cancellationToken);
                    plan = new NtmpBookingSyncPlanner.Plan
                    {
                        TransactionTypeId = plan.TransactionTypeId,
                        CuFlag = "1",
                        LogEventType = plan.LogEventType,
                        StageBits = plan.StageBits,
                        IncludeTransactionId = built.Request.TransactionId != null
                    };
                }

                await ApplyBookingResponseAsync(reservation, plan, built, response, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NTMP SyncBooking failed for reservation {ReservationId}", reservationId);
            }
        }

        public async Task SyncCancelAsync(int reservationId, CancellationToken cancellationToken = default)
        {
            const string eventType = "CancelBooking";
            try
            {
                var reservation = await _db.Reservations
                    .FirstOrDefaultAsync(r => r.ReservationId == reservationId, cancellationToken);
                if (reservation == null)
                {
                    return;
                }

                var settings = await LoadActiveSettingsAsync(reservation.HotelId, cancellationToken);
                if (settings == null)
                {
                    return;
                }

                var passwordResult = _passwordResolver.Resolve(settings);
                var password = passwordResult.Password;
                if (password == null || string.IsNullOrWhiteSpace(reservation.NtmpTransactionId))
                {
                    await LogAsync(
                        reservation,
                        eventType,
                        null,
                        null,
                        "Error",
                        password == null
                            ? passwordResult.ErrorMessage
                            : "Missing NTMP transaction id.",
                        null,
                        null,
                        null,
                        cancellationToken);
                    return;
                }

                var request = new NtmpCancelBookingRequest
                {
                    TransactionId = reservation.NtmpTransactionId,
                    CancelReason = "1",
                    CancelWithCharges = "0",
                    Channel = settings.ChannelName ?? NtmpApiConstants.ChannelName,
                    CuFlag = "1"
                };

                var response = await _gateway.CancelBookingAsync(settings, password, request, cancellationToken);
                await LogAsync(
                    reservation,
                    eventType,
                    response.RawRequest,
                    response.RawResponse,
                    response.Success ? "Success" : "Error",
                    response.ErrorMessage,
                    response.HttpStatusCode,
                    response.CorrelationId,
                    null,
                    cancellationToken);

                reservation.NtmpLastSyncAt = KsaTime.Now;
                reservation.NtmpLastEventType = eventType;
                reservation.NtmpLastStatus = response.Success ? "Success" : "Error";
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NTMP SyncCancel failed for reservation {ReservationId}", reservationId);
            }
        }

        public async Task SyncExpenseAsync(int reservationId, CancellationToken cancellationToken = default)
        {
            const string eventType = "BookingExpense";
            try
            {
                var reservation = await _db.Reservations.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.ReservationId == reservationId, cancellationToken);
                if (reservation == null || string.IsNullOrWhiteSpace(reservation.NtmpTransactionId))
                {
                    return;
                }

                var status = NormalizeStatusKey(reservation.Status);
                if (!status.Contains("checkedout", StringComparison.Ordinal))
                {
                    _logger.LogDebug(
                        "Skipping NTMP BookingExpense for reservation {ReservationId}: checkout not posted yet.",
                        reservationId);
                    return;
                }

                if ((reservation.NtmpSyncedStages & NtmpBookingSyncPlanner.StageCheckOut) == 0)
                {
                    _logger.LogDebug(
                        "Skipping NTMP BookingExpense for reservation {ReservationId}: NTMP checkout stage not synced.",
                        reservationId);
                    return;
                }

                var settings = await LoadActiveSettingsAsync(reservation.HotelId, cancellationToken);
                var passwordResult = settings != null ? _passwordResolver.Resolve(settings) : null;
                var password = passwordResult?.Password;
                if (settings == null || password == null)
                {
                    return;
                }

                var item = new NtmpExpenseItem
                {
                    ExpenseDate = NtmpLookupMapper.FormatYmd(KsaTime.Now),
                    ItemNumber = $"{reservation.ReservationId}{KsaTime.Now:HHmmss}",
                    ExpenseTypeId = "1",
                    UnitPrice = NtmpLookupMapper.FormatAmount(reservation.TotalAmount),
                    Discount = NtmpLookupMapper.FormatAmount(reservation.TotalDiscounts),
                    Vat = NtmpLookupMapper.FormatAmount(reservation.VatAmount),
                    MunicipalityTax = NtmpLookupMapper.FormatAmount(reservation.LodgingTaxAmount),
                    GrandTotal = NtmpLookupMapper.FormatAmount(reservation.TotalAmount),
                    PaymentType = "1",
                    CuFlag = "1"
                };

                var request = new NtmpBookingExpenseRequest
                {
                    TransactionId = reservation.NtmpTransactionId,
                    Channel = settings.ChannelName ?? NtmpApiConstants.ChannelName,
                    ExpenseItems = new List<NtmpExpenseItem> { item }
                };

                var response = await _gateway.BookingExpenseAsync(settings, password, request, cancellationToken);
                await LogAsync(
                    reservation,
                    eventType,
                    response.RawRequest,
                    response.RawResponse,
                    response.Success ? "Success" : "Error",
                    response.ErrorMessage,
                    response.HttpStatusCode,
                    response.CorrelationId,
                    null,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NTMP SyncExpense failed for reservation {ReservationId}", reservationId);
            }
        }

        public async Task SyncOccupancyAsync(int hotelId, CancellationToken cancellationToken = default)
        {
            const string eventType = "OccupancyUpdate";
            try
            {
                var settings = await LoadActiveSettingsAsync(hotelId, cancellationToken);
                var passwordResult = settings != null ? _passwordResolver.Resolve(settings) : null;
                var password = passwordResult?.Password;
                if (settings == null || password == null)
                {
                    return;
                }

                var scopeHotelIds = await ResolveOccupancyHotelIdsAsync(hotelId, cancellationToken);
                var apartments = await _db.Apartments.AsNoTracking()
                    .Where(a => scopeHotelIds.Contains(a.HotelId))
                    .ToListAsync(cancellationToken);

                var occupied = apartments.Count(a =>
                    string.Equals(a.Status, "occupied", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(a.Status, "rented", StringComparison.OrdinalIgnoreCase));
                var maintenance = apartments.Count(a =>
                    string.Equals(a.Status, "maintenance", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(a.Status, "out_of_order", StringComparison.OrdinalIgnoreCase));
                var booked = apartments.Count(a =>
                    string.Equals(a.Status, "reserved", StringComparison.OrdinalIgnoreCase));
                var available = Math.Max(0, apartments.Count - occupied - maintenance - booked);
                var total = occupied + maintenance + booked + available;
                if (total < 1)
                {
                    total = 1;
                    available = Math.Max(0, 1 - occupied - maintenance - booked);
                }

                var channel = string.IsNullOrWhiteSpace(settings.ChannelName)
                    ? NtmpApiConstants.ChannelName
                    : settings.ChannelName.Trim();

                var request = new NtmpOccupancyUpdateRequest
                {
                    UpdateDate = NtmpLookupMapper.FormatYmd(KsaTime.Now),
                    RoomsOccupied = occupied.ToString(),
                    RoomsAvailable = available.ToString(),
                    RoomsBooked = booked.ToString(),
                    RoomsOnMaintenance = maintenance.ToString(),
                    TotalRooms = total.ToString(),
                    Channel = channel
                };

                var response = await _gateway.OccupancyUpdateAsync(settings, password, request, cancellationToken);
                var logHotelId = await ResolveLogHotelZaaerIdAsync(hotelId, cancellationToken);
                var logRow = new IntegrationResponse
                {
                    HotelId = logHotelId,
                    Service = NtmpApiConstants.ServiceName,
                    EventType = eventType,
                    Status = response.Success ? "Success" : "Error",
                    ErrorMessage = response.ErrorMessage,
                    RequestPayload = response.RawRequest,
                    ResponsePayload = response.RawResponse,
                    HttpStatusCode = response.HttpStatusCode,
                    CorrelationId = response.CorrelationId,
                    CreatedAt = KsaTime.Now
                };
                _db.IntegrationResponses.Add(logRow);
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NTMP SyncOccupancy failed for hotel {HotelId}", hotelId);
            }
        }

        public async Task RetryReservationAsync(int reservationId, CancellationToken cancellationToken = default)
        {
            var reservation = await _db.Reservations.AsNoTracking()
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId, cancellationToken);
            if (reservation == null)
            {
                return;
            }

            var status = NormalizeStatusKey(reservation.Status);
            if (status.Contains("cancel", StringComparison.Ordinal))
            {
                await SyncCancelAsync(reservationId, cancellationToken);
                return;
            }

            var stages = reservation.NtmpSyncedStages;
            var hasTxn = !string.IsNullOrWhiteSpace(reservation.NtmpTransactionId);

            if (!hasTxn || (stages & NtmpBookingSyncPlanner.StageBooking) == 0)
            {
                await SyncBookingAsync(reservationId, NtmpBookingOperation.Booking, cancellationToken);
                reservation = await _db.Reservations.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.ReservationId == reservationId, cancellationToken);
                if (reservation == null)
                {
                    return;
                }

                stages = reservation.NtmpSyncedStages;
                hasTxn = !string.IsNullOrWhiteSpace(reservation.NtmpTransactionId);
            }

            status = NormalizeStatusKey(reservation.Status);
            if ((status.Contains("checkedin", StringComparison.Ordinal) || status.Contains("checked_in", StringComparison.Ordinal))
                && (stages & NtmpBookingSyncPlanner.StageCheckIn) == 0)
            {
                await SyncBookingAsync(reservationId, NtmpBookingOperation.CheckIn, cancellationToken);
                reservation = await _db.Reservations.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.ReservationId == reservationId, cancellationToken);
                if (reservation == null)
                {
                    return;
                }

                stages = reservation.NtmpSyncedStages;
            }

            status = NormalizeStatusKey(reservation.Status);
            if ((status.Contains("checkedout", StringComparison.Ordinal) || status.Contains("checked_out", StringComparison.Ordinal))
                && (stages & NtmpBookingSyncPlanner.StageCheckOut) == 0)
            {
                await SyncBookingAsync(reservationId, NtmpBookingOperation.CheckOut, cancellationToken);
            }
        }

        private static string NormalizeStatusKey(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return string.Empty;
            }

            return status.Trim().ToLowerInvariant()
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace("_", string.Empty, StringComparison.Ordinal);
        }

        private async Task ApplyBookingResponseAsync(
            Reservation reservation,
            NtmpBookingSyncPlanner.Plan plan,
            NtmpPayloadBuildResult built,
            NtmpGatewayResponse response,
            CancellationToken cancellationToken)
        {
            if (response.Success && !string.IsNullOrWhiteSpace(response.TransactionId))
            {
                reservation.NtmpTransactionId = response.TransactionId;
            }

            if (response.Success)
            {
                reservation.NtmpSyncedStages |= plan.StageBits;
            }

            reservation.NtmpLastSyncAt = KsaTime.Now;
            reservation.NtmpLastEventType = plan.LogEventType;
            reservation.NtmpLastStatus = response.Success ? "Success" : "Error";

            await LogAsync(
                reservation,
                plan.LogEventType,
                response.RawRequest,
                response.RawResponse,
                response.Success ? "Success" : "Error",
                response.ErrorMessage,
                response.HttpStatusCode,
                response.CorrelationId,
                built.UnitNumber,
                cancellationToken,
                built.GuestName);

            await _db.SaveChangesAsync(cancellationToken);
        }

        private async Task<List<int>> ResolveOccupancyHotelIdsAsync(int hotelId, CancellationToken cancellationToken)
        {
            var ids = new HashSet<int> { hotelId };
            var zaaerFromInternal = await _db.HotelSettings.AsNoTracking()
                .Where(h => h.HotelId == hotelId && h.ZaaerId != null)
                .Select(h => h.ZaaerId!.Value)
                .FirstOrDefaultAsync(cancellationToken);
            if (zaaerFromInternal != 0)
            {
                ids.Add(zaaerFromInternal);
            }

            var internalFromZaaer = await _db.HotelSettings.AsNoTracking()
                .Where(h => h.ZaaerId == hotelId)
                .Select(h => h.HotelId)
                .FirstOrDefaultAsync(cancellationToken);
            if (internalFromZaaer != 0)
            {
                ids.Add(internalFromZaaer);
            }

            return ids.ToList();
        }

        private async Task<int> ResolveLogHotelZaaerIdAsync(int reservationHotelId, CancellationToken cancellationToken)
        {
            var match = await _db.NtmpDetails.AsNoTracking()
                .Where(n => n.HotelId == reservationHotelId || n.ZaaerId == reservationHotelId)
                .Select(n => n.ZaaerId ?? n.HotelId)
                .FirstOrDefaultAsync(cancellationToken);
            if (match != 0)
            {
                return match;
            }

            var zaaerFromInternal = await _db.HotelSettings.AsNoTracking()
                .Where(h => h.HotelId == reservationHotelId && h.ZaaerId != null)
                .Select(h => h.ZaaerId!.Value)
                .FirstOrDefaultAsync(cancellationToken);
            if (zaaerFromInternal != 0)
            {
                return zaaerFromInternal;
            }

            return reservationHotelId;
        }

        private async Task<NtmpDetails?> LoadActiveSettingsAsync(int hotelId, CancellationToken cancellationToken)
        {
            var settings = await _db.NtmpDetails.AsNoTracking()
                .Where(n => n.IsActive && (n.HotelId == hotelId || n.ZaaerId == hotelId))
                .FirstOrDefaultAsync(cancellationToken);
            if (settings != null)
            {
                return settings;
            }

            var zaaerFromInternal = await _db.HotelSettings.AsNoTracking()
                .Where(h => h.HotelId == hotelId && h.ZaaerId != null)
                .Select(h => h.ZaaerId!.Value)
                .FirstOrDefaultAsync(cancellationToken);
            if (zaaerFromInternal == 0)
            {
                return null;
            }

            return await _db.NtmpDetails.AsNoTracking()
                .FirstOrDefaultAsync(
                    n => n.IsActive && (n.HotelId == zaaerFromInternal || n.ZaaerId == zaaerFromInternal),
                    cancellationToken);
        }

        private async Task LogAsync(
            Reservation reservation,
            string eventType,
            string? requestPayload,
            string? responsePayload,
            string status,
            string? errorMessage,
            int? httpStatusCode,
            string? correlationId,
            string? unitNumber,
            CancellationToken cancellationToken,
            string? guest = null)
        {
            var logHotelId = await ResolveLogHotelZaaerIdAsync(reservation.HotelId, cancellationToken);
            _db.IntegrationResponses.Add(new IntegrationResponse
            {
                HotelId = logHotelId,
                ResNo = reservation.ReservationNo,
                Service = NtmpApiConstants.ServiceName,
                EventType = eventType,
                UnitNumber = unitNumber,
                Guest = guest,
                ErrorMessage = errorMessage,
                Status = status,
                RequestPayload = requestPayload,
                ResponsePayload = responsePayload,
                HttpStatusCode = httpStatusCode,
                CorrelationId = correlationId,
                CreatedAt = KsaTime.Now
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}

