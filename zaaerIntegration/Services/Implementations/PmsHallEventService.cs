using DevExpress.Drawing.Printing;
using DevExpress.XtraPrinting;
using DevExpress.XtraPrinting.BarCode;
using DevExpress.XtraReports.UI;
using FinanceLedgerAPI.Enums;
using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Reporting.Abstractions;
using zaaerIntegration.Security;
using zaaerIntegration.Services.ActivityLog;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    public sealed class PmsHallEventService : IPmsHallEventService
    {
        private const string ReceiptStatusPaid = "paid";
        private const string DepositVoucherCode = "hall_deposit";

        private readonly ApplicationDbContext _context;
        private readonly ITenantService _tenantService;
        private readonly INumberingService _numberingService;
        private readonly ICurrentUserContext _currentUser;
        private readonly IReportRenderService _renderService;
        private readonly IHallNotificationService _notifications;
        private readonly IReservationActivityLogWriter _activityLog;

        public PmsHallEventService(
            ApplicationDbContext context,
            ITenantService tenantService,
            INumberingService numberingService,
            ICurrentUserContext currentUser,
            IReportRenderService renderService,
            IHallNotificationService notifications,
            IReservationActivityLogWriter activityLog)
        {
            _context = context;
            _tenantService = tenantService;
            _numberingService = numberingService;
            _currentUser = currentUser;
            _renderService = renderService;
            _notifications = notifications;
            _activityLog = activityLog;
        }

        public async Task<PmsHallEventLookupsDto> GetLookupsAsync(CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var halls = await ListHallsInternalAsync(scope, cancellationToken);
            return new PmsHallEventLookupsDto
            {
                HotelId = scope.ScopeHotelId,
                IsHall = true,
                PropertyType = PropertyTypes.Hall,
                EventTypes = HallEventTypes.All.ToList(),
                EventStatuses = HallEventStatusCodes.ToLookupList()
                    .Select(x => new PmsLookupOptionDto
                    {
                        Value = x.Value,
                        LabelEn = x.LabelEn,
                        LabelAr = x.LabelAr,
                        Color = x.Color
                    }).ToList(),
                GenderTypes = HallGenderTypes.All.ToList(),
                VenueKinds = HallVenueKinds.All.ToList(),
                PreparationStatuses = HallPreparationStatuses.All.ToList(),
                PackagePriceTypes = PackagePriceTypes.All.ToList(),
                PackageCategories = PackageCategories.All.ToList(),
                Halls = halls.ToList()
            };
        }

        public async Task<IReadOnlyList<PmsHallEventListItemDto>> ListEventsAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? eventStatus = null,
            int? hallId = null,
            string? fromDateHijri = null,
            string? toDateHijri = null,
            string? eventDateHijri = null,
            int? hijriYear = null,
            int? hijriMonth = null,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var query = BuildEventJoinQuery(scope);

            var hijriGregorian = HijriDateHelper.ResolveGregorianFilterFromHijriParams(
                fromDateHijri,
                toDateHijri,
                eventDateHijri,
                hijriYear,
                hijriMonth);

            var effectiveFrom = fromDate?.Date;
            if (hijriGregorian.From.HasValue)
            {
                effectiveFrom = effectiveFrom.HasValue
                    ? (effectiveFrom.Value > hijriGregorian.From.Value ? effectiveFrom.Value : hijriGregorian.From.Value)
                    : hijriGregorian.From.Value;
            }

            var effectiveTo = toDate?.Date;
            if (hijriGregorian.To.HasValue)
            {
                effectiveTo = effectiveTo.HasValue
                    ? (effectiveTo.Value < hijriGregorian.To.Value ? effectiveTo.Value : hijriGregorian.To.Value)
                    : hijriGregorian.To.Value;
            }

            if (effectiveFrom.HasValue)
            {
                var from = effectiveFrom.Value;
                query = query.Where(x => x.Profile.EventDate >= from);
            }

            if (effectiveTo.HasValue)
            {
                var to = effectiveTo.Value;
                query = query.Where(x => x.Profile.EventDate <= to);
            }

            if (!string.IsNullOrWhiteSpace(eventStatus))
            {
                var status = eventStatus.Trim().ToLowerInvariant();
                query = query.Where(x => x.Profile.EventStatus == status);
            }

            if (hallId.HasValue)
            {
                query = query.Where(x => x.Profile.HallId == hallId.Value);
            }

            var rows = await query
                .OrderBy(x => x.Profile.EventDate)
                .ThenBy(x => x.Profile.EventStartTime)
                .ToListAsync(cancellationToken);

            return rows.Select(x => MapListItem(ToEventRow(x))).ToList();
        }

        public async Task<PmsHallEventDetailDto?> UpdateEventScheduleAsync(
            int reservationId,
            PmsUpdateHallEventScheduleDto dto,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var reservation = await ResolveReservationByRouteIdAsync(scope, reservationId, cancellationToken)
                ?? throw new ArgumentException("Reservation not found.");

            var profile = await FindEventProfileByRouteIdAsync(scope, reservationId, cancellationToken)
                ?? throw new InvalidOperationException("Hall event profile not found for this reservation.");

            var eventDate = dto.EventDate.Date;
            var start = !string.IsNullOrWhiteSpace(dto.EventStartTime)
                ? ParseTime(dto.EventStartTime)
                : profile.EventStartTime;
            var end = !string.IsNullOrWhiteSpace(dto.EventEndTime)
                ? ParseTime(dto.EventEndTime)
                : profile.EventEndTime;

            if (end <= start)
            {
                throw new ArgumentException("Event end time must be after start time.");
            }

            var linkId = HallReservationLink.GetStorageId(reservation);
            if (!profile.HallId.HasValue)
            {
                throw new InvalidOperationException("Hall event profile has no hall assigned.");
            }

            if (await HasConflictAsync(scope, profile.HallId.Value, eventDate, start, end, linkId, cancellationToken))
            {
                throw new InvalidOperationException("Hall is already booked for the selected date and time.");
            }

            var eventDateHijri = HijriDateHelper.ResolveEventHijri(
                eventDate,
                string.IsNullOrWhiteSpace(dto.EventDateHijri) ? null : dto.EventDateHijri);

            profile.EventDate = eventDate;
            profile.EventDateHijri = eventDateHijri;
            profile.EventStartTime = start;
            profile.EventEndTime = end;

            reservation.CheckInDate = eventDate.Add(start);
            reservation.CheckOutDate = eventDate.Add(end);
            reservation.DepartureDate = eventDate.Add(end);

            var reservationRefs = GetReservationStorageRefs(reservation);
            var units = await _context.ReservationUnits
                .Where(u => reservationRefs.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);
            foreach (var unit in units)
            {
                unit.CheckInDate = eventDate.Add(start);
                unit.CheckOutDate = eventDate.Add(end);
                unit.DepartureDate = eventDate.Add(end);
            }

            var periods = await _context.ReservationPeriods
                .Where(p => reservationRefs.Contains(p.ReservationId))
                .ToListAsync(cancellationToken);
            foreach (var period in periods)
            {
                period.FromDate = eventDate.Add(start);
                period.ToDate = eventDate.Add(end);
            }

            var sheet = await FindFunctionSheetByRouteIdAsync(scope, reservationId, cancellationToken, track: true);
            if (sheet != null)
            {
                sheet.EventDate = eventDate;
                sheet.EventDateHijri = eventDateHijri;
                sheet.ServiceStartTime = start;
                sheet.GuestArrivalTime = start;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return await GetEventAsync(reservationId, cancellationToken);
        }

        public async Task<PmsHallEventDetailDto?> UpdateEventAsync(
            int reservationId,
            PmsUpdateHallEventDto dto,
            CancellationToken cancellationToken = default)
        {
            if (dto.EventDate.HasValue)
            {
                var scheduled = await UpdateEventScheduleAsync(reservationId, new PmsUpdateHallEventScheduleDto
                {
                    EventDate = dto.EventDate.Value,
                    EventDateHijri = dto.EventDateHijri,
                    EventStartTime = dto.EventStartTime,
                    EventEndTime = dto.EventEndTime
                }, cancellationToken);
                if (scheduled == null)
                {
                    return null;
                }
            }

            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var profile = await FindEventProfileByRouteIdAsync(scope, reservationId, cancellationToken)
                ?? throw new InvalidOperationException("Hall event profile not found for this reservation.");

            var changed = false;

            if (!string.IsNullOrWhiteSpace(dto.EventType))
            {
                profile.EventType = NormalizeEventType(dto.EventType);
                changed = true;
            }

            if (dto.ExpectedGuests.HasValue)
            {
                profile.ExpectedGuests = dto.ExpectedGuests.Value;
                changed = true;
            }

            if (dto.OccasionName != null)
            {
                profile.OccasionName = string.IsNullOrWhiteSpace(dto.OccasionName) ? null : dto.OccasionName.Trim();
                changed = true;
            }

            if (dto.HallId.HasValue)
            {
                var hall = await ResolveHallAsync(scope, dto.HallId.Value, cancellationToken)
                    ?? throw new ArgumentException("Hall not found.");
                profile.HallId = hall.ApartmentId;
                changed = true;
            }

            if (!dto.EventDate.HasValue
                && (!string.IsNullOrWhiteSpace(dto.EventStartTime) || !string.IsNullOrWhiteSpace(dto.EventEndTime)))
            {
                var start = !string.IsNullOrWhiteSpace(dto.EventStartTime)
                    ? ParseTime(dto.EventStartTime)
                    : profile.EventStartTime;
                var end = !string.IsNullOrWhiteSpace(dto.EventEndTime)
                    ? ParseTime(dto.EventEndTime)
                    : profile.EventEndTime;
                if (end <= start)
                {
                    throw new ArgumentException("Event end time must be after start time.");
                }

                profile.EventStartTime = start;
                profile.EventEndTime = end;
                changed = true;

                var reservation = await ResolveReservationByRouteIdAsync(scope, reservationId, cancellationToken);
                if (reservation != null)
                {
                    var eventDate = profile.EventDate.Date;
                    reservation.CheckInDate = eventDate.Add(start);
                    reservation.CheckOutDate = eventDate.Add(end);
                    reservation.DepartureDate = eventDate.Add(end);
                }
            }

            if (changed)
            {
                profile.UpdatedAt = KsaTime.Now;
                await _context.SaveChangesAsync(cancellationToken);
            }

            if (dto.HallRentAmount.HasValue || dto.DepositAmount.HasValue)
            {
                await ApplyEventFinancialUpdatesAsync(scope, reservationId, profile, dto, cancellationToken);
            }

            return await GetEventAsync(reservationId, cancellationToken);
        }

        public async Task<PmsHallEventDetailDto?> GetEventAsync(int reservationId, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var row = await BuildEventJoinQuery(scope)
                .Where(x =>
                    x.Reservation.ReservationId == reservationId
                    || x.Reservation.ZaaerId == reservationId)
                .FirstOrDefaultAsync(cancellationToken);
            if (row == null)
            {
                return null;
            }

            var linkId = HallReservationLink.GetStorageId(row.Reservation);
            var detail = MapDetail(ToEventRow(row));
            var sheet = await _context.EventFunctionSheets.AsNoTracking()
                .Include(s => s.Items)
                .FirstOrDefaultAsync(
                    s => (s.ReservationId == linkId || s.ReservationId == row.Reservation.ReservationId)
                        && (s.HotelId == scope.ScopeHotelId || s.HotelId == scope.LocalHotelId),
                    cancellationToken);
            detail.FunctionSheet = sheet == null ? null : MapFunctionSheet(sheet);
            return detail;
        }

        public async Task<PmsHallEventDetailDto> CreateEventAsync(PmsCreateHallEventDto dto, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var hall = await ResolveHallAsync(scope, dto.HallId, cancellationToken)
                ?? throw new ArgumentException("Hall not found.");

            var start = ParseTime(dto.EventStartTime);
            var end = ParseTime(dto.EventEndTime);
            if (end <= start)
            {
                throw new ArgumentException("Event end time must be after start time.");
            }

            var eventDate = dto.EventDate.Date;
            if (await HasConflictAsync(scope, hall.ApartmentId, eventDate, start, end, null, cancellationToken))
            {
                throw new InvalidOperationException("Hall is already booked for the selected date and time.");
            }

            var taxConfig = await ResolveHallEventTaxConfigAsync(scope, cancellationToken);
            var hallGross = dto.HallRentAmount;
            var hallCalc = HotelPricingTaxHelper.CalculateAmounts(hallGross, taxConfig);

            decimal packageNetSum = 0m;
            decimal packageEwaSum = 0m;
            decimal packageVatSum = 0m;
            decimal packageTotalSum = 0m;
            var packageLineCalcs = new List<(PmsHallEventPackageLineDto Line, decimal Gross, (decimal NetAmount, decimal EwaAmount, decimal VatAmount, decimal Total) Calc)>();
            foreach (var line in dto.Packages)
            {
                var lineGross = CalculateLineAmount(line, dto.ExpectedGuests, start, end);
                var lineCalc = HotelPricingTaxHelper.CalculateAmounts(lineGross, taxConfig);
                packageNetSum += lineCalc.NetAmount;
                packageEwaSum += lineCalc.EwaAmount;
                packageVatSum += lineCalc.VatAmount;
                packageTotalSum += lineCalc.Total;
                packageLineCalcs.Add((line, lineGross, lineCalc));
            }

            var subtotal = Math.Round(hallCalc.NetAmount + packageNetSum, 2, MidpointRounding.AwayFromZero);
            var ewaAmount = Math.Round(hallCalc.EwaAmount + packageEwaSum, 2, MidpointRounding.AwayFromZero);
            var vatAmount = Math.Round(hallCalc.VatAmount + packageVatSum, 2, MidpointRounding.AwayFromZero);
            var total = Math.Round(hallCalc.Total + packageTotalSum, 2, MidpointRounding.AwayFromZero);
            var deposit = Math.Min(dto.DepositAmount, total);
            var remaining = total - deposit;

            var reservationIdentity = await _numberingService.GetNextBusinessIdentityAsync(
                NumberingDocCodes.Reservation,
                scope.ScopeHotelId,
                "pms-hall-event",
                $"hall-event:{scope.ScopeHotelId}:{Guid.NewGuid():N}",
                cancellationToken);

            var now = KsaTime.Now;
            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            long? customerAuditId = null;
            try
            {
                var (customerStoredId, newCustomerAuditId) = await ResolveHallEventCustomerAsync(scope, dto, cancellationToken);
                customerAuditId = newCustomerAuditId;
                if (customerStoredId <= 0)
                {
                    throw new InvalidOperationException("Customer could not be resolved for this event booking.");
                }

                var reservation = new Reservation
                {
                    ReservationNo = reservationIdentity.DocumentNo,
                    HotelId = scope.ScopeHotelId,
                    CustomerId = customerStoredId,
                    ReservationDate = now,
                    RentalType = RentalTypeHelper.ToStorageValue(RentalType.InHour),
                    Subtotal = subtotal,
                    VatRate = taxConfig.VatRate,
                    VatAmount = vatAmount,
                    LodgingTaxRate = taxConfig.EwaRate,
                    LodgingTaxAmount = ewaAmount,
                    TotalTaxAmount = Math.Round(ewaAmount + vatAmount, 2, MidpointRounding.AwayFromZero),
                    TotalExtra = Math.Round(packageTotalSum, 2, MidpointRounding.AwayFromZero),
                    TotalAmount = total,
                    AmountPaid = 0,
                    BalanceAmount = total,
                    CheckInDate = eventDate.Add(start),
                    CheckOutDate = eventDate.Add(end),
                    DepartureDate = eventDate.Add(end),
                    Status = ReservationStatusHelper.ToStorageValue(ReservationStatus.Unconfirmed),
                    CreatedBy = _currentUser.UserId,
                    CreatedAt = now,
                    ZaaerId = ZaaerIdMapper.ToNullableInt32(reservationIdentity.ZaaerId)
                };
                _context.Reservations.Add(reservation);
                await _context.SaveChangesAsync(cancellationToken);

                var reservationLinkId = HallReservationLink.GetStorageId(reservation);
                var hallStorageId = GetApartmentStorageId(hall);
                var eventDateHijri = HijriDateHelper.FormatStorageDate(eventDate);

                _context.ReservationUnits.Add(new ReservationUnit
                {
                    ReservationId = reservationLinkId,
                    ApartmentId = hallStorageId,
                    CheckInDate = eventDate.Add(start),
                    CheckOutDate = eventDate.Add(end),
                    DepartureDate = eventDate.Add(end),
                    NumberOfNights = 0,
                    RentAmount = hallCalc.NetAmount,
                    VatRate = taxConfig.VatRate,
                    VatAmount = hallCalc.VatAmount,
                    LodgingTaxRate = taxConfig.EwaRate,
                    LodgingTaxAmount = hallCalc.EwaAmount,
                    TotalAmount = hallCalc.Total,
                    Status = "Reserved"
                });

                _context.ReservationPeriods.Add(new ReservationPeriod
                {
                    ReservationId = reservationLinkId,
                    UnitId = hallStorageId,
                    RentalType = RentalTypeHelper.ToStorageValue(RentalType.InHour),
                    FromDate = eventDate.Add(start),
                    ToDate = eventDate.Add(end),
                    GrossRate = hallGross,
                    TaxIncluded = taxConfig.TaxIncluded,
                    Status = "Active",
                    CreatedAt = now
                });

                var profile = new ReservationEventProfile
                {
                    HotelId = scope.ScopeHotelId,
                    ReservationId = reservationLinkId,
                    HallId = hall.ApartmentId,
                    EventType = NormalizeEventType(dto.EventType),
                    EventDate = eventDate,
                    EventDateHijri = eventDateHijri,
                    EventStartTime = start,
                    EventEndTime = end,
                    ExpectedGuests = dto.ExpectedGuests,
                    OccasionName = dto.OccasionName,
                    OccasionOwner = dto.OccasionOwner,
                    DepositAmount = deposit,
                    RemainingBalance = remaining,
                    EventStatus = HallEventStatusCodes.Unconfirmed,
                    DepositDueAt = dto.DepositDueAt,
                    CreatedAt = now
                };
                _context.ReservationEventProfiles.Add(profile);

                foreach (var (line, _, lineCalc) in packageLineCalcs)
                {
                    _context.ReservationExtras.Add(new ReservationExtra
                    {
                        ReservationId = reservationLinkId,
                        PackageId = line.PackageId,
                        ItemName = line.Name ?? "Package",
                        PostingRule = "OnCheckIn",
                        ServiceDate = eventDate,
                        GuestCount = dto.ExpectedGuests,
                        UnitPrice = line.UnitPrice,
                        Subtotal = lineCalc.NetAmount,
                        TaxAmount = Math.Round(lineCalc.EwaAmount + lineCalc.VatAmount, 2, MidpointRounding.AwayFromZero),
                        TotalAmount = lineCalc.Total,
                        CreatedBy = _currentUser.UserId,
                        CreatedAt = now
                    });
                }

                _context.EventFunctionSheets.Add(new EventFunctionSheet
                {
                    HotelId = scope.ScopeHotelId,
                    ReservationId = reservationLinkId,
                    HallId = hall.ApartmentId,
                    EventDate = eventDate,
                    EventDateHijri = eventDateHijri,
                    ServiceStartTime = start,
                    GuestArrivalTime = start,
                    ExecutionStatus = "draft",
                    CreatedAt = now
                });

                await _context.SaveChangesAsync(cancellationToken);
                await _numberingService.MarkCommittedAsync(reservationIdentity.AuditId, cancellationToken);
                if (customerAuditId.HasValue)
                {
                    await _numberingService.MarkCommittedAsync(customerAuditId.Value, cancellationToken);
                }
                await tx.CommitAsync(cancellationToken);

                return (await GetEventAsync(reservationLinkId, cancellationToken))!;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken);
                if (customerAuditId.HasValue)
                {
                    await _numberingService.MarkVoidedAsync(customerAuditId.Value, ex.Message, cancellationToken);
                }
                throw;
            }
        }

        public async Task<PmsHallEventDetailDto?> TransitionStatusAsync(
            int reservationId,
            PmsTransitionHallEventStatusDto dto,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var profile = await FindEventProfileByRouteIdAsync(scope, reservationId, cancellationToken);
            if (profile == null)
            {
                return null;
            }

            var from = HallEventStatusCodes.FromCode(profile.EventStatus);
            var to = HallEventStatusCodes.FromCode(dto.EventStatus);
            if (!HallEventStatusCodes.CanTransition(from, to))
            {
                throw new InvalidOperationException($"Cannot transition from {HallEventStatusCodes.ToCode(from)} to {HallEventStatusCodes.ToCode(to)}.");
            }

            if (to == HallEventStatus.Confirmed)
            {
                throw new InvalidOperationException("Use check-in to confirm the event.");
            }

            var reservation = await ResolveReservationByRouteIdAsync(scope, reservationId, cancellationToken);
            if (to == HallEventStatus.Closed)
            {
                await EnsureHallEventRentSettledAsync(scope, reservation, reservationId, cancellationToken);
            }

            profile.EventStatus = HallEventStatusCodes.ToCode(to);
            profile.UpdatedAt = KsaTime.Now;
            if (dto.ActualGuests.HasValue)
            {
                profile.ActualGuests = dto.ActualGuests;
            }

            if (!string.IsNullOrWhiteSpace(dto.Notes))
            {
                profile.CompletionNotes = dto.Notes;
            }

            if (reservation != null && to == HallEventStatus.Closed)
            {
                reservation.Status = ReservationStatusHelper.ToStorageValue(ReservationStatus.CheckedOut);
                var reservationRefs = GetReservationStorageRefs(reservation);
                var units = await _context.ReservationUnits
                    .Where(u => reservationRefs.Contains(u.ReservationId))
                    .ToListAsync(cancellationToken);
                foreach (var unit in units)
                {
                    unit.Status = "checked_out";
                }

                if (profile.HallId.HasValue)
                {
                    var hall = await _context.Apartments.FirstOrDefaultAsync(a => a.ApartmentId == profile.HallId.Value, cancellationToken);
                    if (hall != null)
                    {
                        hall.HallPreparationStatus = HallPreparationStatuses.Cleaning;
                    }
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            return await GetEventAsync(reservationId, cancellationToken);
        }

        public async Task<PmsHallEventDetailDto?> CheckInEventAsync(
            int reservationId,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var profile = await FindEventProfileByRouteIdAsync(scope, reservationId, cancellationToken)
                ?? throw new InvalidOperationException("Hall event profile not found for this reservation.");
            var reservation = await ResolveReservationByRouteIdAsync(scope, reservationId, cancellationToken)
                ?? throw new InvalidOperationException("Reservation not found.");

            var current = HallEventStatusCodes.FromCode(profile.EventStatus);
            if (current == HallEventStatus.Closed)
            {
                throw new InvalidOperationException("Cannot check in a closed event.");
            }

            var reservationStatus = ReservationStatusHelper.TryParseStorage(reservation.Status, out var parsedStatus)
                ? parsedStatus
                : ReservationStatus.Unconfirmed;
            if (reservationStatus == ReservationStatus.CheckedIn || reservationStatus == ReservationStatus.CheckedOut)
            {
                profile.EventStatus = ResolveEventStatusCodeFromReservation(reservation);
                profile.UpdatedAt = KsaTime.Now;
                await _context.SaveChangesAsync(cancellationToken);
                return await GetEventAsync(reservationId, cancellationToken);
            }

            profile.EventStatus = HallEventStatusCodes.Confirmed;
            reservation.Status = ReservationStatusHelper.ToStorageValue(ReservationStatus.CheckedIn);
            profile.UpdatedAt = KsaTime.Now;

            var reservationRefs = GetReservationStorageRefs(reservation);
            var units = await _context.ReservationUnits
                .Where(u => reservationRefs.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);
            foreach (var unit in units)
            {
                unit.Status = "checked_in";
            }

            await _context.SaveChangesAsync(cancellationToken);

            await _activityLog.LogAsync(
                new ReservationActivityLogEntry
                {
                    EventKey = ReservationActivityEvents.ReservationCheckIn,
                    HotelId = reservation.HotelId,
                    ReservationId = reservation.ReservationId,
                    ReservationNo = reservation.ReservationNo,
                    RefType = "HallEvent",
                    RefId = profile.EventProfileId,
                    IconKey = "check",
                    Payload = new Dictionary<string, object?>
                    {
                        ["eventStatus"] = profile.EventStatus,
                        ["reservationStatus"] = reservation.Status
                    },
                    ZaaerId = reservation.ZaaerId
                },
                cancellationToken);

            return await GetEventAsync(reservationId, cancellationToken);
        }

        public async Task<PmsHallEventDetailDto?> RecordDepositAsync(
            int reservationId,
            PmsRecordHallDepositDto dto,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var profile = await FindEventProfileByRouteIdAsync(scope, reservationId, cancellationToken);
            var reservation = await ResolveReservationByRouteIdAsync(scope, reservationId, cancellationToken);
            if (profile == null || reservation == null)
            {
                return null;
            }

            var reservationLinkId = HallReservationLink.GetStorageId(reservation);
            var paymentMethod = await _context.PaymentMethods.AsNoTracking()
                .FirstOrDefaultAsync(m => m.PaymentMethodId == dto.PaymentMethodId, cancellationToken)
                ?? throw new ArgumentException("Payment method not found.");

            var customerZaaerId = await _context.Customers.AsNoTracking()
                .Where(c => c.CustomerId == reservation.CustomerId || c.ZaaerId == reservation.CustomerId)
                .Select(c => c.ZaaerId)
                .FirstOrDefaultAsync(cancellationToken);
            if (!customerZaaerId.HasValue || customerZaaerId.Value <= 0)
            {
                throw new ArgumentException("Customer ZaaerId is required for hall deposit receipts.");
            }

            var receiptIdentity = await _numberingService.GetNextBusinessIdentityAsync(
                NumberingDocCodes.PaymentReceipt,
                scope.ScopeHotelId,
                "pms-hall-deposit",
                $"hall-deposit:{reservationId}:{Guid.NewGuid():N}",
                cancellationToken);

            var now = KsaTime.Now;
            var isCash = string.Equals(paymentMethod.MethodName, "Cash", StringComparison.OrdinalIgnoreCase)
                || string.Equals(paymentMethod.MethodName, "نقدي", StringComparison.OrdinalIgnoreCase);

            var receipt = new PaymentReceipt
            {
                HotelId = scope.ScopeHotelId,
                ReceiptNo = receiptIdentity.DocumentNo,
                ReservationId = reservationLinkId,
                CustomerId = customerZaaerId.Value,
                ReceiptDate = now,
                ReceiptType = "receipt",
                VoucherCode = DepositVoucherCode,
                AmountPaid = dto.Amount,
                PaymentMethodId = paymentMethod.PaymentMethodId,
                PaymentMethod = paymentMethod.MethodName,
                BankId = isCash ? null : dto.BankId,
                TransactionNo = isCash ? string.Empty : (dto.TransactionNo ?? string.Empty).Trim(),
                Notes = dto.Notes ?? "Hall event deposit",
                Reason = "Hall event deposit",
                ReceiptFrom = profile.EventDate,
                ReceiptTo = profile.EventDate,
                ReceiptStatus = ReceiptStatusPaid,
                CreatedBy = _currentUser.UserId,
                CreatedAt = now,
                ZaaerId = ZaaerIdMapper.ToNullableInt32(receiptIdentity.ZaaerId)
            };

            _context.PaymentReceipts.Add(receipt);
            profile.DepositAmount += dto.Amount;
            profile.RemainingBalance = Math.Max(0, (reservation.TotalAmount ?? 0) - profile.DepositAmount);
            reservation.AmountPaid = (reservation.AmountPaid ?? 0) + dto.Amount;
            reservation.BalanceAmount = profile.RemainingBalance;

            profile.UpdatedAt = now;
            await _context.SaveChangesAsync(cancellationToken);
            await _numberingService.MarkCommittedAsync(receiptIdentity.AuditId, cancellationToken);

            return await GetEventAsync(reservationId, cancellationToken);
        }

        public async Task<PmsHallEventDetailDto?> CompleteEventAsync(
            int reservationId,
            PmsCompleteHallEventDto dto,
            CancellationToken cancellationToken = default)
        {
            if (!dto.EventCompleted || !dto.HallDelivered || !dto.NoIssues)
            {
                throw new ArgumentException("All completion checklist items must be confirmed.");
            }

            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var profile = await FindEventProfileByRouteIdAsync(scope, reservationId, cancellationToken);
            if (profile == null)
            {
                return null;
            }

            var reservation = await ResolveReservationByRouteIdAsync(scope, reservationId, cancellationToken);
            await EnsureHallEventRentSettledAsync(scope, reservation, reservationId, cancellationToken);

            profile.ActualGuests = dto.ActualGuests ?? profile.ActualGuests ?? profile.ExpectedGuests;
            profile.CompletionNotes = dto.CompletionNotes;
            profile.EventStatus = HallEventStatusCodes.Closed;
            profile.UpdatedAt = KsaTime.Now;

            if (reservation != null)
            {
                reservation.Status = ReservationStatusHelper.ToStorageValue(ReservationStatus.CheckedOut);
                var reservationRefs = GetReservationStorageRefs(reservation);
                var units = await _context.ReservationUnits
                    .Where(u => reservationRefs.Contains(u.ReservationId))
                    .ToListAsync(cancellationToken);
                foreach (var unit in units)
                {
                    unit.Status = "checked_out";
                }
            }

            if (profile.HallId.HasValue)
            {
                var hall = await _context.Apartments.FirstOrDefaultAsync(a => a.ApartmentId == profile.HallId.Value, cancellationToken);
                if (hall != null)
                {
                    hall.HallPreparationStatus = HallPreparationStatuses.Cleaning;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            return await GetEventAsync(reservationId, cancellationToken);
        }

        public async Task<IReadOnlyList<PmsHallSchedulerItemDto>> GetSchedulerItemsAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            var events = await ListEventsAsync(fromDate, toDate, null, null, cancellationToken: cancellationToken);
            return events.Select(e =>
            {
                var start = e.EventDate.Date.Add(ParseTime(e.EventStartTime));
                var end = e.EventDate.Date.Add(ParseTime(e.EventEndTime));
                var status = HallEventStatusCodes.FromCode(e.EventStatus);
                return new PmsHallSchedulerItemDto
                {
                    ReservationId = e.ReservationId,
                    Text = $"{e.OccasionName ?? e.CustomerName ?? e.ReservationNo} ({e.HallName})",
                    StartDate = start,
                    EndDate = end,
                    HallId = e.HallId ?? 0,
                    HallName = e.HallName ?? string.Empty,
                    EventStatus = e.EventStatus,
                    EventStatusColor = HallEventStatusCodes.GetStatusColor(status)
                };
            }).ToList();
        }

        public async Task<PmsHallDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            await SyncOperationalStatusesAsync(cancellationToken);
            var today = KsaTime.Now.Date;
            var tomorrow = today.AddDays(1);
            var monthEnd = today.AddDays(30);
            var horizonEnd = today.AddDays(90);
            var all = await ListEventsAsync(today.AddDays(-7), horizonEnd, null, null, cancellationToken: cancellationToken);

            var hallLinkIds = await _context.ReservationEventProfiles
                .AsNoTracking()
                .Where(p => p.HotelId == scope.ScopeHotelId || p.HotelId == scope.LocalHotelId)
                .Select(p => p.ReservationId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var todayCollections = hallLinkIds.Count == 0
                ? 0m
                : await _context.PaymentReceipts
                    .AsNoTracking()
                    .Where(r => r.ReservationId != null
                        && hallLinkIds.Contains(r.ReservationId.Value)
                        && r.ReceiptDate.Date == today
                        && r.ReceiptType == "receipt")
                    .SumAsync(r => (decimal?)r.AmountPaid, cancellationToken) ?? 0m;

            var todayEvents = all.Where(e => e.EventDate.Date == today).ToList();
            var tomorrowEvents = all.Where(e => e.EventDate.Date == tomorrow).ToList();
            var monthEvents = all.Where(e => e.EventDate.Date >= today && e.EventDate.Date <= monthEnd).ToList();
            var upcomingEvents = all
                .Where(e => e.EventDate.Date >= today
                    && HallEventStatusCodes.FromCode(e.EventStatus) != HallEventStatus.Closed)
                .OrderBy(e => e.EventDate)
                .ThenBy(e => e.EventStartTime)
                .Take(100)
                .ToList();
            var latePayments = all.Where(e => e.RemainingBalance > 0 && e.EventDate.Date >= today).ToList();

            return new PmsHallDashboardDto
            {
                TodayCount = todayEvents.Count,
                TomorrowCount = tomorrowEvents.Count,
                WeekCount = monthEvents.Count,
                LatePaymentCount = latePayments.Count,
                TodayRevenue = todayCollections,
                DepositsTotal = all.Sum(e => e.DepositAmount),
                RemainingBalanceTotal = all.Sum(e => e.RemainingBalance),
                TodayEvents = todayEvents,
                TomorrowEvents = tomorrowEvents,
                UpcomingEvents = upcomingEvents,
                LatePayments = latePayments,
                Alerts = (await ListAlertsAsync(unreadOnly: true, cancellationToken)).ToList()
            };
        }

        public async Task<IReadOnlyList<PmsHallOccupancyCardDto>> GetOccupancyAsync(CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var halls = await ListHallsInternalAsync(scope, cancellationToken);
            var today = KsaTime.Now.Date;
            var horizonEnd = today.AddDays(120);
            var events = await ListEventsAsync(today, horizonEnd, null, null, cancellationToken: cancellationToken);
            var now = KsaTime.Now;

            return halls.Select(h =>
            {
                var hallEvents = events
                    .Where(e => e.HallId == h.HallId
                        && HallEventStatusCodes.FromCode(e.EventStatus) != HallEventStatus.Closed)
                    .OrderBy(e => e.EventDate)
                    .ThenBy(e => e.EventStartTime)
                    .ToList();

                var current = hallEvents.FirstOrDefault(e => e.EventDate.Date == today);
                PmsHallEventListItemDto? next = null;
                string? occupancyState = "vacant";
                string? timeLabel = null;
                string? nextLabel = null;

                if (current != null)
                {
                    occupancyState = "live";
                    var end = current.EventDate.Date.Add(ParseTime(current.EventEndTime));
                    var remaining = end - now;
                    timeLabel = remaining.TotalMinutes > 0
                        ? $"{Math.Floor(remaining.TotalHours)}h {remaining.Minutes}m"
                        : "Ended";
                }
                else
                {
                    next = hallEvents.FirstOrDefault(e => e.EventDate.Date > today);
                    if (next != null)
                    {
                        occupancyState = "upcoming";
                        nextLabel = $"{next.EventDate:yyyy-MM-dd} {next.EventStartTime}";
                    }
                }

                return new PmsHallOccupancyCardDto
                {
                    HallId = h.HallId,
                    HallCode = h.HallCode,
                    HallName = h.HallName,
                    PreparationStatus = h.PreparationStatus,
                    CurrentEvent = current,
                    NextEvent = next,
                    OccupancyState = occupancyState,
                    TimeRemainingLabel = timeLabel,
                    NextEventLabel = nextLabel
                };
            }).ToList();
        }

        public async Task SyncOperationalStatusesAsync(CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            await ReconcileHallDepositsFromReceiptsAsync(scope, cancellationToken);

            var profiles = await _context.ReservationEventProfiles
                .Where(p => p.HotelId == scope.ScopeHotelId || p.HotelId == scope.LocalHotelId)
                .ToListAsync(cancellationToken);

            var reservations = await _context.Reservations
                .Where(r => r.HotelId == scope.ScopeHotelId || r.HotelId == scope.LocalHotelId)
                .ToListAsync(cancellationToken);

            var reservationByLink = reservations
                .GroupBy(HallReservationLink.GetStorageId)
                .ToDictionary(g => g.Key, g => g.First());

            var today = KsaTime.Now.Date;
            var now = KsaTime.Now;

            foreach (var profile in profiles)
            {
                if (reservationByLink.TryGetValue(profile.ReservationId, out var reservation))
                {
                    var code = ResolveEventStatusCodeFromReservation(reservation);
                    if (!string.Equals(profile.EventStatus, code, StringComparison.OrdinalIgnoreCase))
                    {
                        profile.EventStatus = code;
                        profile.UpdatedAt = now;
                    }
                }

                if (profile.EventDate != today
                    || HallEventStatusCodes.FromCode(profile.EventStatus) != HallEventStatus.Confirmed
                    || !profile.HallId.HasValue)
                {
                    continue;
                }

                var start = profile.EventDate.Date.Add(profile.EventStartTime);
                var end = profile.EventDate.Date.Add(profile.EventEndTime);
                if (now >= start && now <= end)
                {
                    var hall = await _context.Apartments.FirstOrDefaultAsync(
                        a => a.ApartmentId == profile.HallId.Value,
                        cancellationToken);
                    if (hall != null)
                    {
                        hall.HallPreparationStatus = HallPreparationStatuses.Occupied;
                    }
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await GenerateAlertsAsync(cancellationToken);
        }

        public async Task<PmsFunctionSheetDto?> GetFunctionSheetAsync(int reservationId, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var sheet = await FindFunctionSheetByRouteIdAsync(scope, reservationId, cancellationToken, track: false);
            return sheet == null ? null : MapFunctionSheet(sheet);
        }

        public async Task<PmsFunctionSheetDto> UpsertFunctionSheetAsync(int reservationId, PmsFunctionSheetDto dto, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var profile = await FindEventProfileByRouteIdAsync(scope, reservationId, cancellationToken)
                ?? throw new InvalidOperationException("Event not found.");

            var reservation = await ResolveReservationByRouteIdAsync(scope, reservationId, cancellationToken);
            var reservationLinkId = reservation != null
                ? HallReservationLink.GetStorageId(reservation)
                : profile.ReservationId;

            var sheet = await FindFunctionSheetByRouteIdAsync(scope, reservationId, cancellationToken, track: true);

            var now = KsaTime.Now;
            if (sheet == null)
            {
                sheet = new EventFunctionSheet
                {
                    HotelId = scope.ScopeHotelId,
                    ReservationId = reservationLinkId,
                    HallId = profile.HallId,
                    EventDate = profile.EventDate,
                    EventDateHijri = profile.EventDateHijri ?? HijriDateHelper.FormatStorageDate(profile.EventDate),
                    CreatedAt = now
                };
                _context.EventFunctionSheets.Add(sheet);
            }

            sheet.HallOpenTime = ParseNullableTime(dto.HallOpenTime);
            sheet.GuestArrivalTime = ParseNullableTime(dto.GuestArrivalTime) ?? profile.EventStartTime;
            sheet.ServiceStartTime = ParseNullableTime(dto.ServiceStartTime) ?? profile.EventStartTime;
            sheet.CoffeeType = dto.CoffeeType;
            sheet.MenuNotes = dto.MenuNotes;
            sheet.DecorationNotes = dto.DecorationNotes;
            sheet.SoundAvNotes = dto.SoundAvNotes;
            sheet.CoordinatorUserId = dto.CoordinatorUserId;
            sheet.ClientSpecialRequests = dto.ClientSpecialRequests;
            sheet.ExecutionStatus = string.IsNullOrWhiteSpace(dto.ExecutionStatus) ? "draft" : dto.ExecutionStatus.Trim().ToLowerInvariant();
            sheet.UpdatedAt = now;

            _context.EventFunctionSheetItems.RemoveRange(sheet.Items);
            sheet.Items = (dto.Items ?? new List<PmsFunctionSheetItemDto>())
                .Select((item, index) => new EventFunctionSheetItem
                {
                    Category = item.Category,
                    ItemName = item.ItemName,
                    Quantity = item.Quantity,
                    Notes = item.Notes,
                    SortOrder = item.SortOrder > 0 ? item.SortOrder : index
                }).ToList();

            await _context.SaveChangesAsync(cancellationToken);
            return MapFunctionSheet(sheet);
        }

        public async Task<PmsFunctionSheetDto?> ApproveFunctionSheetAsync(int reservationId, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var sheet = await FindFunctionSheetByRouteIdAsync(scope, reservationId, cancellationToken, track: true);
            if (sheet == null)
            {
                return null;
            }

            var now = KsaTime.Now;
            sheet.ExecutionStatus = "approved";
            sheet.ApprovedBy = _currentUser.UserId;
            sheet.ApprovedAt = now;
            sheet.UpdatedAt = now;
            await _context.SaveChangesAsync(cancellationToken);
            return MapFunctionSheet(sheet);
        }

        public async Task<ReportRenderResult?> PrintFunctionSheetAsync(int reservationId, CancellationToken cancellationToken = default)
        {
            var detail = await GetEventAsync(reservationId, cancellationToken);
            var sheet = detail?.FunctionSheet ?? await GetFunctionSheetAsync(reservationId, cancellationToken);
            if (detail == null || sheet == null)
            {
                return null;
            }

            var report = BuildDocumentReport("Function Sheet / ورقة التشغيل", detail, sheet);
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var entity = await FindFunctionSheetByRouteIdAsync(scope, reservationId, cancellationToken, track: true);
            if (entity != null)
            {
                entity.PrintedAt = KsaTime.Now;
                await _context.SaveChangesAsync(cancellationToken);
            }

            return await _renderService.ExportToPdfAsync(report, $"function-sheet-{detail.ReservationNo}", cancellationToken);
        }

        public async Task<ReportRenderResult?> PrintContractAsync(int reservationId, CancellationToken cancellationToken = default)
        {
            var detail = await GetEventAsync(reservationId, cancellationToken);
            if (detail == null)
            {
                return null;
            }

            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var profile = await FindEventProfileByRouteIdAsync(scope, reservationId, cancellationToken);
            if (profile != null)
            {
                profile.ContractSigned = true;
                profile.ContractSignedAt = KsaTime.Now;
                await _context.SaveChangesAsync(cancellationToken);
            }

            var report = BuildContractReport(detail);
            return await _renderService.ExportToPdfAsync(report, $"hall-contract-{detail.ReservationNo}", cancellationToken);
        }

        public async Task<IReadOnlyList<PmsHallEventAlertDto>> ListAlertsAsync(bool unreadOnly = false, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var query = _context.HallEventAlerts.AsNoTracking()
                .Where(a => a.HotelId == scope.ScopeHotelId || a.HotelId == scope.LocalHotelId);
            if (unreadOnly)
            {
                query = query.Where(a => !a.IsRead);
            }

            return await query
                .OrderByDescending(a => a.CreatedAt)
                .Take(100)
                .Select(a => new PmsHallEventAlertDto
                {
                    AlertId = a.AlertId,
                    ReservationId = a.ReservationId,
                    AlertType = a.AlertType,
                    Message = a.Message,
                    Severity = a.Severity,
                    IsRead = a.IsRead,
                    DueAt = a.DueAt,
                    CreatedAt = a.CreatedAt
                })
                .ToListAsync(cancellationToken);
        }

        public async Task MarkAlertReadAsync(int alertId, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var alert = await _context.HallEventAlerts
                .FirstOrDefaultAsync(a => a.AlertId == alertId && (a.HotelId == scope.ScopeHotelId || a.HotelId == scope.LocalHotelId), cancellationToken);
            if (alert == null)
            {
                return;
            }

            alert.IsRead = true;
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task GenerateAlertsAsync(CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var now = KsaTime.Now;
            var tomorrow = now.Date.AddDays(1);
            var events = await _context.ReservationEventProfiles.AsNoTracking()
                .Where(p => (p.HotelId == scope.ScopeHotelId || p.HotelId == scope.LocalHotelId)
                    && p.EventStatus != HallEventStatusCodes.Closed
                    && p.EventStatus != "completed"
                    && p.EventStatus != "cancelled")
                .ToListAsync(cancellationToken);

            foreach (var profile in events)
            {
                if (profile.RemainingBalance > 0)
                {
                    await EnsureAlertAsync(scope.ScopeHotelId, profile.ReservationId, "balance_due",
                        $"Remaining balance {profile.RemainingBalance:N2} SAR for reservation.", "warning", null, cancellationToken);
                    await _notifications.NotifyBalanceDueAsync(profile.ReservationId, cancellationToken);
                }

                if (profile.EventDate.Date == tomorrow)
                {
                    await EnsureAlertAsync(scope.ScopeHotelId, profile.ReservationId, "event_tomorrow",
                        "Event scheduled for tomorrow.", "info", tomorrow, cancellationToken);
                    await _notifications.NotifyEventTomorrowAsync(profile.ReservationId, cancellationToken);
                }

                if (profile.DepositDueAt.HasValue && profile.DepositDueAt.Value < now && profile.DepositAmount <= 0)
                {
                    await EnsureAlertAsync(scope.ScopeHotelId, profile.ReservationId, "deposit_expiry",
                        "Deposit due date has passed.", "danger", profile.DepositDueAt, cancellationToken);
                    await _notifications.NotifyDepositDueAsync(profile.ReservationId, cancellationToken);
                }
            }
        }

        public async Task<PmsHallDailyEventsReportDto> GetDailyEventsReportAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            var events = await ListEventsAsync(date.Date, date.Date, null, null, cancellationToken: cancellationToken);
            return new PmsHallDailyEventsReportDto
            {
                ReportDate = date.Date,
                Events = events.ToList(),
                TotalRevenue = events.Sum(e => e.TotalAmount),
                TotalDeposits = events.Sum(e => e.DepositAmount),
                TotalBalanceDue = events.Sum(e => e.RemainingBalance)
            };
        }

        public async Task<PmsHallUtilizationReportDto> GetUtilizationReportAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var halls = await ListHallsInternalAsync(scope, cancellationToken);
            var events = await ListEventsAsync(fromDate, toDate, null, null, cancellationToken: cancellationToken);
            var days = Math.Max(1, (toDate.Date - fromDate.Date).Days + 1);
            var availableHoursPerHall = days * 14m;

            var lines = halls.Select(h =>
            {
                var hallEvents = events.Where(e => e.HallId == h.HallId).ToList();
                var booked = hallEvents.Sum(e =>
                {
                    var start = ParseTime(e.EventStartTime);
                    var end = ParseTime(e.EventEndTime);
                    return (decimal)(end - start).TotalHours;
                });
                var utilization = availableHoursPerHall > 0 ? Math.Round(booked / availableHoursPerHall * 100m, 1) : 0;
                return new PmsHallUtilizationLineDto
                {
                    HallId = h.HallId,
                    HallName = h.HallName ?? h.HallCode,
                    BookedHours = booked,
                    AvailableHours = availableHoursPerHall,
                    UtilizationPercent = utilization,
                    EventCount = hallEvents.Count
                };
            }).ToList();

            return new PmsHallUtilizationReportDto
            {
                FromDate = fromDate.Date,
                ToDate = toDate.Date,
                Lines = lines
            };
        }

        public async Task<bool> UpdateHallPreparationAsync(int hallId, PmsUpdateHallPreparationDto dto, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var hall = await _context.Apartments
                .FirstOrDefaultAsync(a => a.ApartmentId == hallId && (a.HotelId == scope.ScopeHotelId || a.HotelId == scope.LocalHotelId), cancellationToken);
            if (hall == null)
            {
                return false;
            }

            var status = dto.PreparationStatus.Trim().ToLowerInvariant();
            if (!HallPreparationStatuses.All.Contains(status))
            {
                throw new ArgumentException("Invalid preparation status.");
            }

            hall.HallPreparationStatus = status;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        private async Task EnsureAlertAsync(
            int hotelId,
            int reservationId,
            string alertType,
            string message,
            string severity,
            DateTime? dueAt,
            CancellationToken cancellationToken)
        {
            var exists = await _context.HallEventAlerts.AnyAsync(
                a => a.HotelId == hotelId && a.ReservationId == reservationId && a.AlertType == alertType && !a.IsRead,
                cancellationToken);
            if (exists)
            {
                return;
            }

            _context.HallEventAlerts.Add(new HallEventAlert
            {
                HotelId = hotelId,
                ReservationId = reservationId,
                AlertType = alertType,
                Message = message,
                Severity = severity,
                DueAt = dueAt,
                CreatedAt = KsaTime.Now
            });
            await _context.SaveChangesAsync(cancellationToken);
        }

        private XtraReport BuildDocumentReport(string title, PmsHallEventDetailDto detail, PmsFunctionSheetDto sheet)
        {
            var report = new XtraReport { PaperKind = DXPaperKind.A4, Margins = new System.Drawing.Printing.Margins(40, 40, 40, 40) };
            var detailBand = new DetailBand { HeightF = 600 };
            report.Bands.Add(detailBand);

            var titleLabel = new XRLabel
            {
                Text = title,
                Font = new System.Drawing.Font("Arial", 16, System.Drawing.FontStyle.Bold),
                LocationF = new System.Drawing.PointF(0, 0),
                SizeF = new System.Drawing.SizeF(700, 30)
            };
            detailBand.Controls.Add(titleLabel);

            var body = new XRLabel
            {
                Multiline = true,
                Text = $"Reservation: {detail.ReservationNo}\n" +
                       $"Occasion: {detail.OccasionName}\n" +
                       $"Date: {detail.EventDate:yyyy-MM-dd} {detail.EventStartTime}-{detail.EventEndTime}\n" +
                       $"Hall: {detail.HallName}\n" +
                       $"Guests: {detail.ExpectedGuests}\n\n" +
                       $"Coffee: {sheet.CoffeeType}\n" +
                       $"Menu: {sheet.MenuNotes}\n" +
                       $"Decoration: {sheet.DecorationNotes}\n" +
                       $"Sound/AV: {sheet.SoundAvNotes}\n" +
                       $"Special requests: {sheet.ClientSpecialRequests}",
                LocationF = new System.Drawing.PointF(0, 40),
                SizeF = new System.Drawing.SizeF(700, 400)
            };
            detailBand.Controls.Add(body);
            return report;
        }

        private XtraReport BuildContractReport(PmsHallEventDetailDto detail)
        {
            var report = new XtraReport { PaperKind = DXPaperKind.A4, Margins = new System.Drawing.Printing.Margins(40, 40, 40, 40) };
            var detailBand = new DetailBand { HeightF = 700 };
            report.Bands.Add(detailBand);

            var title = new XRLabel
            {
                Text = "Hall Event Contract / عقد حجز قاعة",
                Font = new System.Drawing.Font("Arial", 16, System.Drawing.FontStyle.Bold),
                LocationF = new System.Drawing.PointF(0, 0),
                SizeF = new System.Drawing.SizeF(700, 30)
            };
            detailBand.Controls.Add(title);

            var qr = new XRBarCode
            {
                Symbology = new QRCodeGenerator(),
                Text = $"HALL-CONTRACT:{detail.ReservationId}:{detail.ReservationNo}",
                LocationF = new System.Drawing.PointF(0, 40),
                SizeF = new System.Drawing.SizeF(100, 100)
            };
            detailBand.Controls.Add(qr);

            var body = new XRLabel
            {
                Multiline = true,
                Text = $"Contract No: {detail.ReservationNo}\n" +
                       $"Customer: {detail.CustomerName}\n" +
                       $"Occasion: {detail.OccasionName}\n" +
                       $"Owner: {detail.OccasionOwner}\n" +
                       $"Event Date: {detail.EventDate:yyyy-MM-dd}\n" +
                       $"Time: {detail.EventStartTime} - {detail.EventEndTime}\n" +
                       $"Hall: {detail.HallName}\n" +
                       $"Guests: {detail.ExpectedGuests}\n" +
                       $"Total: {detail.TotalAmount:N2} SAR\n" +
                       $"Deposit: {detail.DepositAmount:N2} SAR\n" +
                       $"Balance: {detail.RemainingBalance:N2} SAR",
                LocationF = new System.Drawing.PointF(120, 40),
                SizeF = new System.Drawing.SizeF(580, 300)
            };
            detailBand.Controls.Add(body);
            return report;
        }

        private IQueryable<EventJoinRow> BuildEventJoinQuery(HallScope scope)
        {
            var reservationLinks = _context.Reservations.AsNoTracking()
                .Where(r => r.HotelId == scope.ScopeHotelId || r.HotelId == scope.LocalHotelId)
                .Select(r => new
                {
                    LinkId = r.ZaaerId != null && r.ZaaerId > 0 ? r.ZaaerId.Value : r.ReservationId,
                    Reservation = r
                });

            var customerLinks = _context.Customers.AsNoTracking()
                .Select(c => new
                {
                    LinkId = c.ZaaerId != null && c.ZaaerId > 0 ? c.ZaaerId.Value : c.CustomerId,
                    Customer = c
                });

            return
                from profile in _context.ReservationEventProfiles.AsNoTracking()
                where profile.HotelId == scope.ScopeHotelId || profile.HotelId == scope.LocalHotelId
                join resLink in reservationLinks on profile.ReservationId equals resLink.LinkId
                join custLink in customerLinks on resLink.Reservation.CustomerId equals custLink.LinkId into custLinks
                from custLink in custLinks.DefaultIfEmpty()
                join hall in _context.Apartments.AsNoTracking() on profile.HallId equals hall.ApartmentId into halls
                from hall in halls.DefaultIfEmpty()
                select new EventJoinRow
                {
                    Profile = profile,
                    Reservation = resLink.Reservation,
                    Customer = custLink != null ? custLink.Customer : null,
                    Hall = hall
                };
        }

        private static EventRow ToEventRow(EventJoinRow row) =>
            new(row.Reservation, row.Profile, row.Customer, row.Hall);

        private static PmsHallEventListItemDto MapListItem(EventRow row)
        {
            var statusCode = ResolveEventStatusCodeFromReservation(row.Reservation);
            var status = HallEventStatusCodes.FromCode(statusCode);
            return new PmsHallEventListItemDto
            {
                ReservationId = HallReservationLink.GetStorageId(row.Reservation),
                ZaaerId = row.Reservation.ZaaerId,
                ReservationNo = row.Reservation.ReservationNo,
                CustomerId = row.Reservation.CustomerId,
                CustomerName = row.Customer?.CustomerName,
                HallId = row.Profile.HallId,
                HallName = row.Hall?.ApartmentName ?? row.Hall?.ApartmentCode,
                EventType = row.Profile.EventType,
                EventDate = row.Profile.EventDate,
                EventDateHijri = HijriDateHelper.ResolveEventHijri(row.Profile.EventDate, row.Profile.EventDateHijri),
                EventDateHijriDisplay = HijriDateHelper.FormatDate(row.Profile.EventDate),
                EventStartTime = FormatTime(row.Profile.EventStartTime),
                EventEndTime = FormatTime(row.Profile.EventEndTime),
                ExpectedGuests = row.Profile.ExpectedGuests,
                ActualGuests = row.Profile.ActualGuests,
                OccasionName = row.Profile.OccasionName,
                OccasionOwner = row.Profile.OccasionOwner,
                EventStatus = statusCode,
                EventStatusLabelEn = HallEventStatusCodes.GetDisplayNameEn(status),
                EventStatusLabelAr = HallEventStatusCodes.GetDisplayNameAr(status),
                EventStatusColor = HallEventStatusCodes.GetStatusColor(status),
                DepositAmount = row.Profile.DepositAmount,
                RemainingBalance = row.Profile.RemainingBalance,
                TotalAmount = row.Reservation.TotalAmount ?? 0,
                ContractSigned = row.Profile.ContractSigned,
                ReservationStatus = row.Reservation.Status,
                AllowedTransitions = HallEventStatusCodes.GetAllowedTransitionCodes(status).ToList()
            };
        }

        private static PmsHallEventDetailDto MapDetail(EventRow row)
        {
            var item = new PmsHallEventDetailDto();
            var mapped = MapListItem(row);
            item.ReservationId = mapped.ReservationId;
            item.ZaaerId = mapped.ZaaerId;
            item.ReservationNo = mapped.ReservationNo;
            item.CustomerId = mapped.CustomerId;
            item.CustomerName = mapped.CustomerName;
            item.HallId = mapped.HallId;
            item.HallName = mapped.HallName;
            item.EventType = mapped.EventType;
            item.EventDate = mapped.EventDate;
            item.EventStartTime = mapped.EventStartTime;
            item.EventEndTime = mapped.EventEndTime;
            item.ExpectedGuests = mapped.ExpectedGuests;
            item.ActualGuests = mapped.ActualGuests;
            item.OccasionName = mapped.OccasionName;
            item.OccasionOwner = mapped.OccasionOwner;
            item.EventStatus = mapped.EventStatus;
            item.EventStatusLabelEn = mapped.EventStatusLabelEn;
            item.EventStatusLabelAr = mapped.EventStatusLabelAr;
            item.EventStatusColor = mapped.EventStatusColor;
            item.DepositAmount = mapped.DepositAmount;
            item.RemainingBalance = mapped.RemainingBalance;
            item.TotalAmount = mapped.TotalAmount;
            item.ContractSigned = mapped.ContractSigned;
            item.ReservationStatus = mapped.ReservationStatus;
            item.AllowedTransitions = mapped.AllowedTransitions;
            item.OccasionOwner = row.Profile.OccasionOwner;
            item.CompletionNotes = row.Profile.CompletionNotes;
            item.DepositDueAt = row.Profile.DepositDueAt;
            item.ContractSignedAt = row.Profile.ContractSignedAt;
            item.EventDateHijri = mapped.EventDateHijri;
            item.EventDateHijriDisplay = mapped.EventDateHijriDisplay;
            return item;
        }

        private static PmsFunctionSheetDto MapFunctionSheet(EventFunctionSheet sheet) => new()
        {
            FunctionSheetId = sheet.FunctionSheetId,
            ReservationId = sheet.ReservationId,
            HallId = sheet.HallId,
            EventDate = sheet.EventDate,
            HallOpenTime = FormatNullableTime(sheet.HallOpenTime),
            GuestArrivalTime = FormatNullableTime(sheet.GuestArrivalTime),
            ServiceStartTime = FormatNullableTime(sheet.ServiceStartTime),
            CoffeeType = sheet.CoffeeType,
            MenuNotes = sheet.MenuNotes,
            DecorationNotes = sheet.DecorationNotes,
            SoundAvNotes = sheet.SoundAvNotes,
            CoordinatorUserId = sheet.CoordinatorUserId,
            ClientSpecialRequests = sheet.ClientSpecialRequests,
            ExecutionStatus = sheet.ExecutionStatus,
            PrintedAt = sheet.PrintedAt,
            ApprovedAt = sheet.ApprovedAt,
            Items = sheet.Items.OrderBy(i => i.SortOrder).Select(i => new PmsFunctionSheetItemDto
            {
                ItemId = i.ItemId,
                Category = i.Category,
                ItemName = i.ItemName,
                Quantity = i.Quantity,
                Notes = i.Notes,
                SortOrder = i.SortOrder
            }).ToList()
        };

        private async Task<IReadOnlyList<PmsHallListItemDto>> ListHallsInternalAsync(HallScope scope, CancellationToken cancellationToken)
        {
            return await (
                from a in _context.Apartments.AsNoTracking()
                where (a.HotelId == scope.ScopeHotelId || a.HotelId == scope.LocalHotelId)
                    && (a.IsActive == null || a.IsActive == true)
                join rt0 in _context.RoomTypes.AsNoTracking()
                    on a.RoomTypeId equals (int?)rt0.RoomTypeId into rtGroup
                from rt in rtGroup.DefaultIfEmpty()
                orderby a.ApartmentCode
                select new PmsHallListItemDto
                {
                    HallId = a.ApartmentId,
                    ZaaerId = a.ZaaerId,
                    HallCode = a.ApartmentCode,
                    HallName = a.ApartmentName ?? a.ApartmentCode,
                    RoomTypeId = a.RoomTypeId,
                    RoomTypeName = rt != null ? rt.RoomTypeName : null,
                    HallGenderType = rt != null ? rt.HallGenderType : null,
                    HallCapacity = rt != null ? rt.HallCapacity : null,
                    PreparationStatus = a.HallPreparationStatus ?? HallPreparationStatuses.Ready,
                    IsActive = a.IsActive ?? true
                }).ToListAsync(cancellationToken);
        }

        private async Task<Apartment?> ResolveHallAsync(HallScope scope, int hallId, CancellationToken cancellationToken)
        {
            var row = await _context.Apartments.AsNoTracking()
                .Where(a => a.ApartmentId == hallId
                    && (a.HotelId == scope.ScopeHotelId || a.HotelId == scope.LocalHotelId)
                    && (a.IsActive == null || a.IsActive == true))
                .Select(a => new Apartment
                {
                    ApartmentId = a.ApartmentId,
                    HotelId = a.HotelId,
                    ApartmentCode = a.ApartmentCode,
                    ApartmentName = a.ApartmentName ?? a.ApartmentCode,
                    RoomTypeId = a.RoomTypeId,
                    IsActive = a.IsActive ?? true
                })
                .FirstOrDefaultAsync(cancellationToken);

            return row;
        }

        private async Task<bool> HasConflictAsync(
            HallScope scope,
            int hallId,
            DateTime eventDate,
            TimeSpan start,
            TimeSpan end,
            int? excludeReservationId,
            CancellationToken cancellationToken)
        {
            var query = _context.ReservationEventProfiles.AsNoTracking()
                .Where(p => (p.HotelId == scope.ScopeHotelId || p.HotelId == scope.LocalHotelId)
                    && p.HallId == hallId
                    && p.EventDate == eventDate
                    && p.EventStatus != HallEventStatusCodes.Closed
                    && p.EventStatus != "completed"
                    && p.EventStatus != "cancelled");

            if (excludeReservationId.HasValue)
            {
                var excludeIds = new List<int> { excludeReservationId.Value };
                var excludedReservation = await ResolveReservationByRouteIdAsync(scope, excludeReservationId.Value, cancellationToken);
                if (excludedReservation != null)
                {
                    excludeIds.Add(excludedReservation.ReservationId);
                    excludeIds.Add(HallReservationLink.GetStorageId(excludedReservation));
                }

                excludeIds = excludeIds.Distinct().ToList();
                query = query.Where(p => !excludeIds.Contains(p.ReservationId));
            }

            var existing = await query.ToListAsync(cancellationToken);
            return existing.Any(p => TimesOverlap(start, end, p.EventStartTime, p.EventEndTime));
        }

        private static bool TimesOverlap(TimeSpan aStart, TimeSpan aEnd, TimeSpan bStart, TimeSpan bEnd) =>
            aStart < bEnd && bStart < aEnd;

        private async Task<HotelPricingTaxConfig> ResolveHallEventTaxConfigAsync(
            HallScope scope,
            CancellationToken cancellationToken)
        {
            foreach (var hotelId in new[] { scope.ScopeHotelId, scope.LocalHotelId })
            {
                var hasTaxes = await _context.Taxes.AsNoTracking()
                    .AnyAsync(t => t.HotelId == hotelId && t.Enabled, cancellationToken);
                if (!hasTaxes)
                {
                    continue;
                }

                var config = await HotelPricingTaxHelper.GetConfigAsync(_context, hotelId, cancellationToken);
                return config.VatRate > 0m
                    ? config
                    : config with { VatRate = 15m, VatIncluded = true };
            }

            var fallback = await HotelPricingTaxHelper.GetConfigAsync(_context, scope.ScopeHotelId, cancellationToken);
            return fallback.VatRate > 0m
                ? fallback
                : fallback with { VatRate = 15m, VatIncluded = true };
        }

        private static decimal CalculateLineAmount(PmsHallEventPackageLineDto line, int guests, TimeSpan start, TimeSpan end)
        {
            var hours = (decimal)Math.Max(1, (end - start).TotalHours);
            return line.PriceType switch
            {
                var pt when pt == PackagePriceTypes.PerGuest => line.UnitPrice * line.Quantity * guests,
                var pt when pt == PackagePriceTypes.PerHour => line.UnitPrice * line.Quantity * hours,
                _ => line.UnitPrice * line.Quantity
            };
        }

        private static string NormalizeEventType(string? value)
        {
            var normalized = value?.Trim().ToLowerInvariant();
            return HallEventTypes.All.Contains(normalized) ? normalized! : HallEventTypes.Wedding;
        }

        private static int CustomerStorageId(Customer customer) =>
            customer.ZaaerId is > 0 ? customer.ZaaerId.Value : customer.CustomerId;

        private async Task<Customer?> FindHallEventCustomerAsync(
            HallScope scope,
            int customerRouteId,
            CancellationToken cancellationToken)
        {
            var byScopeHotel = await _context.Customers.AsNoTracking()
                .FirstOrDefaultAsync(
                    c => (c.CustomerId == customerRouteId || c.ZaaerId == customerRouteId)
                        && (c.HotelId == scope.ScopeHotelId || c.HotelId == scope.LocalHotelId),
                    cancellationToken);
            if (byScopeHotel != null)
            {
                return byScopeHotel;
            }

            return await _context.Customers.AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.CustomerId == customerRouteId || c.ZaaerId == customerRouteId,
                    cancellationToken);
        }

        private async Task<(int CustomerStoredId, long? NewCustomerAuditId)> ResolveHallEventCustomerAsync(
            HallScope scope,
            PmsCreateHallEventDto dto,
            CancellationToken cancellationToken)
        {
            if (dto.CustomerId.HasValue && dto.CustomerId.Value > 0)
            {
                var existing = await FindHallEventCustomerAsync(scope, dto.CustomerId.Value, cancellationToken)
                    ?? throw new ArgumentException("Customer not found.");

                if (existing.HotelId != scope.ScopeHotelId && existing.HotelId != scope.LocalHotelId)
                {
                    throw new ArgumentException("Customer not found.");
                }

                return (CustomerStorageId(existing), null);
            }

            var customerName = !string.IsNullOrWhiteSpace(dto.OccasionOwner)
                ? dto.OccasionOwner.Trim()
                : !string.IsNullOrWhiteSpace(dto.OccasionName)
                    ? dto.OccasionName.Trim()
                    : "Guest";

            if (customerName.Length > 200)
            {
                customerName = customerName[..200];
            }

            var customerIdentity = await _numberingService.GetNextBusinessIdentityAsync(
                NumberingDocCodes.Customer,
                scope.ScopeHotelId,
                "pms-hall-event-customer",
                $"hall-event-customer:{scope.ScopeHotelId}:{Guid.NewGuid():N}",
                cancellationToken);

            var customer = new Customer
            {
                CustomerNo = customerIdentity.DocumentNo,
                CustomerName = customerName,
                HotelId = scope.ScopeHotelId,
                ZaaerId = ZaaerIdMapper.ToNullableInt32(customerIdentity.ZaaerId),
                IsActive = true,
                Comments = "hall-event",
                CreatedAt = KsaTime.Now,
                EnteredAt = KsaTime.Now,
                EnteredBy = PmsCurrentUser.ResolveUserId(_currentUser)
            };
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync(cancellationToken);

            return (CustomerStorageId(customer), customerIdentity.AuditId);
        }

        private static TimeSpan ParseTime(string? value)
        {
            if (TimeSpan.TryParse(value, out var ts))
            {
                return ts;
            }

            return new TimeSpan(18, 0, 0);
        }

        private static TimeSpan? ParseNullableTime(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : ParseTime(value);

        private static string FormatTime(TimeSpan value) => value.ToString(@"hh\:mm");

        private static string? FormatNullableTime(TimeSpan? value) => value.HasValue ? FormatTime(value.Value) : null;

        private Task<Reservation?> ResolveReservationByRouteIdAsync(
            HallScope scope,
            int routeId,
            CancellationToken cancellationToken) =>
            _context.Reservations.FirstOrDefaultAsync(
                r => (r.ReservationId == routeId || r.ZaaerId == routeId)
                    && (r.HotelId == scope.ScopeHotelId || r.HotelId == scope.LocalHotelId),
                cancellationToken);

        private async Task<ReservationEventProfile?> FindEventProfileByRouteIdAsync(
            HallScope scope,
            int routeId,
            CancellationToken cancellationToken)
        {
            var reservation = await ResolveReservationByRouteIdAsync(scope, routeId, cancellationToken);
            if (reservation == null)
            {
                return await _context.ReservationEventProfiles.FirstOrDefaultAsync(
                    p => p.ReservationId == routeId
                        && (p.HotelId == scope.ScopeHotelId || p.HotelId == scope.LocalHotelId),
                    cancellationToken);
            }

            var linkId = HallReservationLink.GetStorageId(reservation);
            return await _context.ReservationEventProfiles.FirstOrDefaultAsync(
                p => (p.ReservationId == linkId || p.ReservationId == reservation.ReservationId)
                    && (p.HotelId == scope.ScopeHotelId || p.HotelId == scope.LocalHotelId),
                cancellationToken);
        }

        private async Task<EventFunctionSheet?> FindFunctionSheetByRouteIdAsync(
            HallScope scope,
            int routeId,
            CancellationToken cancellationToken,
            bool track)
        {
            var reservation = await ResolveReservationByRouteIdAsync(scope, routeId, cancellationToken);
            var linkIds = new List<int> { routeId };
            if (reservation != null)
            {
                linkIds.Add(HallReservationLink.GetStorageId(reservation));
                if (!linkIds.Contains(reservation.ReservationId))
                {
                    linkIds.Add(reservation.ReservationId);
                }
            }

            IQueryable<EventFunctionSheet> query = track
                ? _context.EventFunctionSheets
                : _context.EventFunctionSheets.AsNoTracking();
            query = query.Include(s => s.Items);

            return await query.FirstOrDefaultAsync(
                s => linkIds.Contains(s.ReservationId)
                    && (s.HotelId == scope.ScopeHotelId || s.HotelId == scope.LocalHotelId),
                cancellationToken);
        }

        private async Task ApplyEventFinancialUpdatesAsync(
            HallScope scope,
            int reservationId,
            ReservationEventProfile profile,
            PmsUpdateHallEventDto dto,
            CancellationToken cancellationToken)
        {
            var reservation = await ResolveReservationByRouteIdAsync(scope, reservationId, cancellationToken)
                ?? throw new InvalidOperationException("Reservation not found.");
            var linkId = HallReservationLink.GetStorageId(reservation);
            var reservationRefs = GetReservationStorageRefs(reservation);
            var now = KsaTime.Now;
            var changed = false;

            if (dto.HallRentAmount.HasValue)
            {
                var taxConfig = await ResolveHallEventTaxConfigAsync(scope, cancellationToken);
                var hallGross = Math.Max(0, dto.HallRentAmount.Value);
                var hallCalc = HotelPricingTaxHelper.CalculateAmounts(hallGross, taxConfig);

                var packageNetSum = await _context.ReservationExtras
                    .AsNoTracking()
                    .Where(e => reservationRefs.Contains(e.ReservationId))
                    .SumAsync(e => e.Subtotal, cancellationToken);
                var packageTaxSum = await _context.ReservationExtras
                    .AsNoTracking()
                    .Where(e => reservationRefs.Contains(e.ReservationId))
                    .SumAsync(e => e.TaxAmount, cancellationToken);
                var packageTotalSum = await _context.ReservationExtras
                    .AsNoTracking()
                    .Where(e => reservationRefs.Contains(e.ReservationId))
                    .SumAsync(e => e.TotalAmount, cancellationToken);

                var subtotal = Math.Round(hallCalc.NetAmount + packageNetSum, 2, MidpointRounding.AwayFromZero);
                var ewaAmount = Math.Round(hallCalc.EwaAmount + packageTaxSum, 2, MidpointRounding.AwayFromZero);
                var vatAmount = Math.Round(hallCalc.VatAmount, 2, MidpointRounding.AwayFromZero);
                var total = Math.Round(hallCalc.Total + packageTotalSum, 2, MidpointRounding.AwayFromZero);

                reservation.Subtotal = subtotal;
                reservation.VatRate = taxConfig.VatRate;
                reservation.VatAmount = vatAmount;
                reservation.LodgingTaxRate = taxConfig.EwaRate;
                reservation.LodgingTaxAmount = ewaAmount;
                reservation.TotalTaxAmount = Math.Round(ewaAmount + vatAmount, 2, MidpointRounding.AwayFromZero);
                reservation.TotalExtra = Math.Round(packageTotalSum, 2, MidpointRounding.AwayFromZero);
                reservation.TotalAmount = total;
                changed = true;

                var units = await _context.ReservationUnits
                    .Where(u => reservationRefs.Contains(u.ReservationId))
                    .ToListAsync(cancellationToken);
                foreach (var unit in units)
                {
                    unit.RentAmount = hallCalc.NetAmount;
                    unit.VatRate = taxConfig.VatRate;
                    unit.VatAmount = hallCalc.VatAmount;
                    unit.LodgingTaxRate = taxConfig.EwaRate;
                    unit.LodgingTaxAmount = hallCalc.EwaAmount;
                    unit.TotalAmount = hallCalc.Total;
                }

                var periods = await _context.ReservationPeriods
                    .Where(p => reservationRefs.Contains(p.ReservationId))
                    .ToListAsync(cancellationToken);
                foreach (var period in periods)
                {
                    period.GrossRate = hallGross;
                }
            }

            if (dto.DepositAmount.HasValue)
            {
                var total = reservation.TotalAmount ?? 0;
                profile.DepositAmount = Math.Max(0, Math.Min(dto.DepositAmount.Value, total));
                changed = true;
            }

            await SyncReservationPaidBalanceFromReceiptsAsync(reservation, profile, linkId, cancellationToken);
            profile.UpdatedAt = now;
            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task SyncReservationPaidBalanceFromReceiptsAsync(
            Reservation reservation,
            ReservationEventProfile profile,
            int reservationLinkId,
            CancellationToken cancellationToken)
        {
            var collected = await _context.PaymentReceipts
                .AsNoTracking()
                .Where(r => r.ReservationId == reservationLinkId
                    && (r.ReceiptStatus == null || r.ReceiptStatus != "cancelled"))
                .SumAsync(r => r.AmountPaid > 0 ? r.AmountPaid : 0m, cancellationToken);

            var total = reservation.TotalAmount ?? 0;
            var balance = Math.Max(0, total - collected);

            reservation.AmountPaid = collected;
            reservation.BalanceAmount = balance;
            profile.RemainingBalance = balance;
        }

        private async Task ReconcileHallDepositsFromReceiptsAsync(HallScope scope, CancellationToken cancellationToken)
        {
            var profiles = await _context.ReservationEventProfiles
                .Where(p => p.HotelId == scope.ScopeHotelId || p.HotelId == scope.LocalHotelId)
                .ToListAsync(cancellationToken);

            if (profiles.Count == 0)
            {
                return;
            }

            var linkIds = profiles.Select(p => p.ReservationId).Distinct().ToList();
            var receiptSums = await _context.PaymentReceipts
                .AsNoTracking()
                .Where(r => r.ReservationId != null
                    && linkIds.Contains(r.ReservationId.Value)
                    && r.ReceiptType == "receipt")
                .GroupBy(r => r.ReservationId!.Value)
                .Select(g => new { ReservationId = g.Key, Total = g.Sum(r => r.AmountPaid) })
                .ToListAsync(cancellationToken);

            var sumByLink = receiptSums.ToDictionary(x => x.ReservationId, x => x.Total);
            var reservations = await _context.Reservations
                .Where(r => r.HotelId == scope.ScopeHotelId || r.HotelId == scope.LocalHotelId)
                .ToListAsync(cancellationToken);
            var reservationByLink = reservations
                .GroupBy(HallReservationLink.GetStorageId)
                .ToDictionary(g => g.Key, g => g.First());

            var now = KsaTime.Now;
            foreach (var profile in profiles)
            {
                if (!sumByLink.TryGetValue(profile.ReservationId, out var paid) || paid <= 0)
                {
                    continue;
                }

                var changed = false;
                if (profile.DepositAmount != paid)
                {
                    profile.DepositAmount = paid;
                    changed = true;
                }

                if (reservationByLink.TryGetValue(profile.ReservationId, out var reservation))
                {
                    var total = reservation.TotalAmount ?? 0;
                    var remaining = Math.Max(0, total - paid);
                    if (profile.RemainingBalance != remaining)
                    {
                        profile.RemainingBalance = remaining;
                        changed = true;
                    }

                    if (reservation.AmountPaid != paid)
                    {
                        reservation.AmountPaid = paid;
                    }

                    if (reservation.BalanceAmount != remaining)
                    {
                        reservation.BalanceAmount = remaining;
                    }
                }

                if (changed)
                {
                    profile.UpdatedAt = now;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private static string ResolveEventStatusCodeFromReservation(Reservation reservation)
        {
            if (!ReservationStatusHelper.TryParseStorage(reservation.Status, out var status))
            {
                return HallEventStatusCodes.Unconfirmed;
            }

            return status switch
            {
                ReservationStatus.CheckedOut => HallEventStatusCodes.Closed,
                ReservationStatus.CheckedIn => HallEventStatusCodes.Confirmed,
                _ => HallEventStatusCodes.Unconfirmed
            };
        }

        private async Task<HallScope> GetCurrentHallScopeAsync(CancellationToken cancellationToken)
        {
            var tenant = _tenantService.GetTenant()
                ?? throw new UnauthorizedAccessException("Tenant not resolved. Provide X-Hotel-Code.");
            var code = tenant.Code.Trim();
            var hotel = await _context.HotelSettings.AsNoTracking()
                .Where(h => h.HotelCode != null)
                .FirstOrDefaultAsync(h => h.HotelCode!.ToLower() == code.ToLower(), cancellationToken)
                ?? throw new InvalidOperationException($"HotelSettings not found for code: {tenant.Code}");

            var propertyType = hotel.PropertyType?.Trim().ToLowerInvariant() ?? PropertyTypes.Hotel;
            if (!PropertyTypes.IsHall(propertyType))
            {
                throw new InvalidOperationException("Hall events are available only for hall properties.");
            }

            return new HallScope(hotel.HotelId, hotel.ZaaerId ?? hotel.HotelId);
        }

        public async Task<PmsHallUnpaidBalancesPageDto> GetUnpaidBalancesAsync(
            int skip = 0,
            int take = 50,
            bool countOnly = false,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            skip = Math.Max(0, skip);
            take = Math.Clamp(take, 1, 200);

            var eventRows = await BuildEventJoinQuery(scope)
                .Where(x => x.Profile.EventStatus != "cancelled")
                .Select(x => new HallEventSettlementSourceRow
                {
                    LinkId = x.Profile.ReservationId,
                    ReservationId = x.Reservation.ReservationId,
                    ZaaerId = x.Reservation.ZaaerId,
                    ReservationNo = x.Reservation.ReservationNo ?? string.Empty,
                    TotalAmount = x.Reservation.TotalAmount ?? 0m,
                    AmountPaid = x.Reservation.AmountPaid ?? 0m,
                    BalanceAmount = x.Reservation.BalanceAmount ?? 0m,
                    CustomerName = x.Customer != null ? x.Customer.CustomerName : null,
                    HallName = x.Hall != null ? (x.Hall.ApartmentName ?? x.Hall.ApartmentCode) : null,
                    EventDate = x.Profile.EventDate,
                    EventStatus = x.Profile.EventStatus ?? string.Empty
                })
                .ToListAsync(cancellationToken);

            if (eventRows.Count == 0)
            {
                return new PmsHallUnpaidBalancesPageDto();
            }

            // Narrow voucher verification to plausible unpaid rows (closed + DB-settled skipped).
            var voucherCheckRows = eventRows.Where(ShouldVerifyHallEventSettlement).ToList();
            if (voucherCheckRows.Count == 0)
            {
                return new PmsHallUnpaidBalancesPageDto();
            }

            var allKeys = new HashSet<int>();
            foreach (var row in voucherCheckRows)
            {
                foreach (var key in HallEventSettlementHelper.BuildReservationLinkKeys(row.ReservationId, row.ZaaerId))
                {
                    allKeys.Add(key);
                }

                allKeys.Add(row.LinkId);
            }

            var receipts = await _context.PaymentReceipts.AsNoTracking()
                .Where(pr => pr.ReservationId.HasValue && allKeys.Contains(pr.ReservationId.Value))
                .ToListAsync(cancellationToken);

            var creditNotes = await _context.CreditNotes.AsNoTracking()
                .Where(cn => cn.ReservationId.HasValue && allKeys.Contains(cn.ReservationId.Value))
                .ToListAsync(cancellationToken);

            var unpaid = new List<PmsHallUnpaidBalanceItemDto>(Math.Min(voucherCheckRows.Count, 256));
            foreach (var row in voucherCheckRows)
            {
                var keys = HallEventSettlementHelper.BuildReservationLinkKeys(row.ReservationId, row.ZaaerId);
                keys.Add(row.LinkId);

                var settlement = ComputeHallEventSettlement(
                    row,
                    receipts.Where(pr => pr.ReservationId.HasValue && keys.Contains(pr.ReservationId.Value)),
                    creditNotes.Where(cn => cn.ReservationId.HasValue && keys.Contains(cn.ReservationId.Value)));

                if (HallEventSettlementHelper.CanCloseEvent(settlement.BalanceDue))
                {
                    continue;
                }

                unpaid.Add(new PmsHallUnpaidBalanceItemDto
                {
                    ReservationId = HallReservationLink.GetStorageId(new Reservation
                    {
                        ReservationId = row.ReservationId,
                        ZaaerId = row.ZaaerId
                    }),
                    ZaaerId = row.ZaaerId,
                    ReservationNo = row.ReservationNo,
                    CustomerName = row.CustomerName,
                    HallName = row.HallName,
                    EventDate = row.EventDate,
                    EventStatus = row.EventStatus,
                    TotalAmount = settlement.TotalAmount,
                    ReceivedAmount = settlement.ReceivedAmount,
                    DisbursedAmount = settlement.DisbursedAmount,
                    BalanceDue = settlement.BalanceDue
                });
            }

            unpaid = unpaid
                .OrderByDescending(x => x.BalanceDue)
                .ThenBy(x => x.EventDate)
                .ToList();

            if (countOnly)
            {
                return new PmsHallUnpaidBalancesPageDto
                {
                    TotalCount = unpaid.Count,
                    Items = new List<PmsHallUnpaidBalanceItemDto>()
                };
            }

            return new PmsHallUnpaidBalancesPageDto
            {
                TotalCount = unpaid.Count,
                Items = unpaid.Skip(skip).Take(take).ToList()
            };
        }

        public async Task<PmsHallEventSettlementDto?> GetSettlementAsync(
            int reservationId,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var row = await BuildEventJoinQuery(scope)
                .Where(x =>
                    x.Reservation.ReservationId == reservationId
                    || x.Reservation.ZaaerId == reservationId)
                .Select(x => new HallEventSettlementSourceRow
                {
                    LinkId = x.Profile.ReservationId,
                    ReservationId = x.Reservation.ReservationId,
                    ZaaerId = x.Reservation.ZaaerId,
                    ReservationNo = x.Reservation.ReservationNo ?? string.Empty,
                    TotalAmount = x.Reservation.TotalAmount ?? 0m,
                    AmountPaid = x.Reservation.AmountPaid ?? 0m,
                    BalanceAmount = x.Reservation.BalanceAmount ?? 0m,
                    CustomerName = x.Customer != null ? x.Customer.CustomerName : null,
                    HallName = x.Hall != null ? (x.Hall.ApartmentName ?? x.Hall.ApartmentCode) : null,
                    EventDate = x.Profile.EventDate,
                    EventStatus = x.Profile.EventStatus ?? string.Empty
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (row == null)
            {
                return null;
            }

            var keys = HallEventSettlementHelper.BuildReservationLinkKeys(row.ReservationId, row.ZaaerId);
            keys.Add(row.LinkId);

            var receipts = await _context.PaymentReceipts.AsNoTracking()
                .Where(pr => pr.ReservationId.HasValue && keys.Contains(pr.ReservationId.Value))
                .ToListAsync(cancellationToken);

            var creditNotes = await _context.CreditNotes.AsNoTracking()
                .Where(cn => cn.ReservationId.HasValue && keys.Contains(cn.ReservationId.Value))
                .ToListAsync(cancellationToken);

            var settlement = ComputeHallEventSettlement(row, receipts, creditNotes);
            return new PmsHallEventSettlementDto
            {
                ReservationId = HallReservationLink.GetStorageId(new Reservation
                {
                    ReservationId = row.ReservationId,
                    ZaaerId = row.ZaaerId
                }),
                ReservationNo = row.ReservationNo,
                TotalAmount = settlement.TotalAmount,
                ReceivedAmount = settlement.ReceivedAmount,
                DisbursedAmount = settlement.DisbursedAmount,
                BalanceDue = settlement.BalanceDue,
                ReservationAmountPaid = row.AmountPaid,
                ReservationBalanceAmount = row.BalanceAmount,
                CanClose = HallEventSettlementHelper.CanCloseEvent(settlement.BalanceDue)
            };
        }

        private async Task EnsureHallEventRentSettledAsync(
            HallScope scope,
            Reservation? reservation,
            int routeReservationId,
            CancellationToken cancellationToken)
        {
            if (reservation == null)
            {
                throw new InvalidOperationException("Reservation not found for this hall event.");
            }

            var keys = HallEventSettlementHelper.BuildReservationLinkKeys(
                reservation.ReservationId,
                reservation.ZaaerId);
            keys.Add(HallReservationLink.GetStorageId(reservation));

            var receipts = await _context.PaymentReceipts.AsNoTracking()
                .Where(pr => pr.ReservationId.HasValue && keys.Contains(pr.ReservationId.Value))
                .ToListAsync(cancellationToken);

            var creditNotes = await _context.CreditNotes.AsNoTracking()
                .Where(cn => cn.ReservationId.HasValue && keys.Contains(cn.ReservationId.Value))
                .ToListAsync(cancellationToken);

            var source = new HallEventSettlementSourceRow
            {
                LinkId = HallReservationLink.GetStorageId(reservation),
                ReservationId = reservation.ReservationId,
                ZaaerId = reservation.ZaaerId,
                ReservationNo = reservation.ReservationNo ?? string.Empty,
                TotalAmount = reservation.TotalAmount ?? 0m,
                AmountPaid = reservation.AmountPaid ?? 0m,
                BalanceAmount = reservation.BalanceAmount ?? 0m
            };

            var settlement = ComputeHallEventSettlement(source, receipts, creditNotes);
            if (HallEventSettlementHelper.CanCloseEvent(settlement.BalanceDue))
            {
                return;
            }

            var reservationNo = string.IsNullOrWhiteSpace(reservation.ReservationNo)
                ? routeReservationId.ToString()
                : reservation.ReservationNo;

            throw new InvalidOperationException(
                $"Cannot close hall event {reservationNo}: outstanding rent balance {settlement.BalanceDue:N2} remains after vouchers (total {settlement.TotalAmount:N2}, received {settlement.ReceivedAmount:N2}, disbursed {settlement.DisbursedAmount:N2}).");
        }

        private static bool ShouldVerifyHallEventSettlement(HallEventSettlementSourceRow row)
        {
            if (row.TotalAmount <= 0.01m)
            {
                return false;
            }

            // Bulk list: verify vouchers only when reservation totals hint at outstanding rent.
            // Close/settlement endpoints always run full voucher math regardless of these fields.
            return row.BalanceAmount > 0.01m || row.AmountPaid < row.TotalAmount - 0.01m;
        }

        private static HallEventSettlementComputed ComputeHallEventSettlement(
            HallEventSettlementSourceRow row,
            IEnumerable<PaymentReceipt> receipts,
            IEnumerable<CreditNote> creditNotes)
        {
            HallEventSettlementHelper.AccumulateRentReceipts(receipts, out var received, out var disbursedReceipts);
            var disbursed = Math.Round(
                disbursedReceipts + HallEventSettlementHelper.SumCreditNoteDisbursements(creditNotes),
                2,
                MidpointRounding.AwayFromZero);
            received = Math.Round(received, 2, MidpointRounding.AwayFromZero);
            var total = Math.Round(row.TotalAmount, 2, MidpointRounding.AwayFromZero);
            var balance = HallEventSettlementHelper.ComputeBalanceDue(total, received, disbursed);

            return new HallEventSettlementComputed
            {
                TotalAmount = total,
                ReceivedAmount = received,
                DisbursedAmount = disbursed,
                BalanceDue = balance
            };
        }

        private static int GetApartmentStorageId(Apartment apartment) =>
            apartment.ZaaerId is > 0 ? apartment.ZaaerId.Value : apartment.ApartmentId;

        private static IReadOnlyList<int> GetReservationStorageRefs(Reservation reservation) =>
            ReservationPeriodStorage.GetReservationStorageRefs(reservation);

        private sealed record HallScope(int LocalHotelId, int ScopeHotelId);
        private sealed record EventRow(Reservation Reservation, ReservationEventProfile Profile, Customer? Customer, Apartment? Hall);

        private sealed class HallEventSettlementSourceRow
        {
            public int LinkId { get; set; }
            public int ReservationId { get; set; }
            public int? ZaaerId { get; set; }
            public string ReservationNo { get; set; } = string.Empty;
            public decimal TotalAmount { get; set; }
            public decimal AmountPaid { get; set; }
            public decimal BalanceAmount { get; set; }
            public string? CustomerName { get; set; }
            public string? HallName { get; set; }
            public DateTime EventDate { get; set; }
            public string EventStatus { get; set; } = string.Empty;
        }

        private sealed class HallEventSettlementComputed
        {
            public decimal TotalAmount { get; set; }
            public decimal ReceivedAmount { get; set; }
            public decimal DisbursedAmount { get; set; }
            public decimal BalanceDue { get; set; }
        }

        private sealed class EventJoinRow
        {
            public ReservationEventProfile Profile { get; set; } = null!;
            public Reservation Reservation { get; set; } = null!;
            public Customer? Customer { get; set; }
            public Apartment? Hall { get; set; }
        }
    }
}
