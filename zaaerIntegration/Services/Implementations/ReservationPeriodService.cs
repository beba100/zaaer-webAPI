#pragma warning disable CS1591

using FinanceLedgerAPI.Enums;
using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms.ReservationDetail;
using zaaerIntegration.Security;
using zaaerIntegration.Services.ActivityLog;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    public sealed class ReservationPeriodService : IReservationPeriodService
    {
        private readonly ApplicationDbContext _context;
        private readonly IReservationDetailService _reservationDetailService;
        private readonly ReservationPermissionGuard _permissionGuard;
        private readonly IReservationActivityLogWriter _activityLog;

        public ReservationPeriodService(
            ApplicationDbContext context,
            IReservationDetailService reservationDetailService,
            ReservationPermissionGuard permissionGuard,
            IReservationActivityLogWriter activityLog)
        {
            _context = context;
            _reservationDetailService = reservationDetailService;
            _permissionGuard = permissionGuard;
            _activityLog = activityLog;
        }

        public async Task<ReservationPeriodListResponseDto?> GetPeriodsAsync(
            int routeId,
            int? hotelId,
            CancellationToken cancellationToken = default)
        {
            var reservation = await FindReservationAsync(routeId, hotelId, cancellationToken);
            if (reservation == null)
            {
                return null;
            }

            return await BuildListResponseAsync(reservation, cancellationToken);
        }

        public async Task<ReservationPeriodListResponseDto?> CreateInitialPeriodAsync(
            int routeId,
            int? hotelId,
            CancellationToken cancellationToken = default)
        {
            await _permissionGuard.EnsureAsync("reservations.rental_periods", cancellationToken);

            var reservation = await FindReservationTrackedAsync(routeId, hotelId, cancellationToken);
            if (reservation == null)
            {
                return null;
            }

            var resRefs = ReservationPeriodStorage.GetReservationStorageRefs(reservation);
            var existing = await _context.ReservationPeriods
                .AnyAsync(p => resRefs.Contains(p.ReservationId), cancellationToken);
            if (existing)
            {
                return await BuildListResponseAsync(reservation, cancellationToken);
            }

            var units = await LoadReservationUnitsAsync(reservation, cancellationToken);
            if (units.Count == 0)
            {
                throw new InvalidOperationException("reservationDetail.periods.noUnits");
            }

            var unit = units[0];
            var apartment = await LoadApartmentForUnitAsync(reservation, unit, cancellationToken);
            var checkIn = (reservation.CheckInDate ?? unit.CheckInDate).Date;
            var checkOut = (reservation.CheckOutDate ?? unit.CheckOutDate).Date;
            if (checkOut <= checkIn)
            {
                checkOut = checkIn.AddDays(1);
            }

            var gross = await ResolveInitialPeriodGrossAsync(reservation, unit, cancellationToken);
            var today = KsaTime.Now.Date;
            var status = checkOut <= today
                ? ReservationPeriodStatus.Closed
                : ReservationPeriodStatus.Active;

            var period = new ReservationPeriod
            {
                ReservationId = ReservationPeriodStorage.GetStorageReservationId(reservation),
                UnitId = ReservationPeriodStorage.GetStorageUnitId(unit, apartment),
                RentalType = ReservationPeriodDayRateGenerator.NormalizeRentalType(reservation.RentalType),
                FromDate = checkIn,
                ToDate = checkOut,
                GrossRate = gross,
                TaxIncluded = true,
                Status = status,
                CreatedAt = KsaTime.Now
            };

            _context.ReservationPeriods.Add(period);
            await _context.SaveChangesAsync(cancellationToken);

            return await BuildListResponseAsync(reservation, cancellationToken);
        }

        public async Task<ReservationPeriodAppendResultDto?> AppendPeriodAsync(
            int routeId,
            ReservationPeriodAppendRequestDto request,
            int? hotelId,
            CancellationToken cancellationToken = default)
        {
            await _permissionGuard.EnsureAsync("reservations.rental_periods", cancellationToken);

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var reservation = await FindReservationTrackedAsync(routeId, hotelId, cancellationToken);
            if (reservation == null)
            {
                return null;
            }

            var rentalNorm = ReservationPeriodDayRateGenerator.NormalizeRentalType(request.RentalType);
            if (string.IsNullOrWhiteSpace(rentalNorm))
            {
                throw new ArgumentException("RentalType is required.");
            }

            var units = await LoadReservationUnitsAsync(reservation, cancellationToken);
            if (units.Count == 0)
            {
                throw new InvalidOperationException("reservationDetail.periods.noUnits");
            }

            ReservationUnit targetUnit;
            if (request.UnitId.HasValue && request.UnitId.Value > 0)
            {
                targetUnit = await ResolveUnitByRouteOrStorageIdAsync(
                          units,
                          reservation,
                          request.UnitId.Value,
                          cancellationToken)
                    ?? throw new InvalidOperationException("reservationDetail.periods.unitNotFound");
            }
            else
            {
                targetUnit = units[0];
            }

            var targetApartment = await LoadApartmentForUnitAsync(reservation, targetUnit, cancellationToken);
            var storageUnitId = ReservationPeriodStorage.GetStorageUnitId(targetUnit, targetApartment);

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var resRefs = ReservationPeriodStorage.GetReservationStorageRefs(reservation);
                var periods = await _context.ReservationPeriods
                    .Where(p => resRefs.Contains(p.ReservationId))
                    .OrderBy(p => p.FromDate)
                    .ToListAsync(cancellationToken);

                if (periods.Count == 0)
                {
                    await SeedInitialPeriodInTransactionAsync(
                        reservation,
                        targetUnit,
                        targetApartment,
                        periods,
                        cancellationToken);
                }

                if (request.ClosePreviousPeriod)
                {
                    foreach (var p in periods.Where(p =>
                                 string.Equals(p.Status, ReservationPeriodStatus.Active, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (!request.UnitId.HasValue
                            || p.UnitId == null
                            || ReservationPeriodStorage.PeriodMatchesUnit(p, targetUnit, targetApartment))
                        {
                            p.Status = ReservationPeriodStatus.Closed;
                            p.UpdatedAt = KsaTime.Now;
                        }
                    }
                }

                var lastPeriod = periods
                    .Where(p => p.UnitId == null || ReservationPeriodStorage.PeriodMatchesUnit(p, targetUnit, targetApartment))
                    .OrderByDescending(p => p.ToDate)
                    .FirstOrDefault();

                var toDate = ResolveSegmentToDate(request);
                var fromDate = request.FromDate?.Date
                    ?? (lastPeriod != null ? lastPeriod.ToDate.Date : (reservation.CheckOutDate ?? targetUnit.CheckOutDate).Date);

                if (toDate <= fromDate)
                {
                    throw new InvalidOperationException("reservationDetail.periods.invalidDateRange");
                }

                if (lastPeriod != null && fromDate < lastPeriod.ToDate.Date && request.ClosePreviousPeriod)
                {
                    throw new InvalidOperationException("reservationDetail.periods.overlapPrevious");
                }

                var gross = request.GrossRate;
                if (!gross.HasValue || gross.Value <= 0m)
                {
                    gross = await ResolveDefaultGrossForPeriodAsync(
                        reservation,
                        targetUnit,
                        rentalNorm,
                        fromDate,
                        cancellationToken);
                }

                if (gross <= 0m)
                {
                    throw new InvalidOperationException("reservationDetail.periods.noGrossRate");
                }

                var newPeriod = new ReservationPeriod
                {
                    ReservationId = ReservationPeriodStorage.GetStorageReservationId(reservation),
                    UnitId = storageUnitId,
                    RentalType = rentalNorm,
                    FromDate = fromDate,
                    ToDate = toDate,
                    GrossRate = Math.Round(gross.Value, 2, MidpointRounding.AwayFromZero),
                    TaxIncluded = true,
                    Status = ReservationPeriodStatus.Active,
                    CreatedAt = KsaTime.Now
                };

                _context.ReservationPeriods.Add(newPeriod);
                periods.Add(newPeriod);
                await _context.SaveChangesAsync(cancellationToken);

                ApplyCheckoutExtension(reservation, targetUnit, toDate);

                reservation.RentalType = rentalNorm;
                if (ReservationPeriodDayRateGenerator.IsMonthlyRental(rentalNorm))
                {
                    reservation.NumberOfMonths = Math.Max(1, reservation.NumberOfMonths ?? 1);
                    reservation.TotalNights = null;
                }
                else
                {
                    reservation.NumberOfMonths = null;
                    reservation.TotalNights = ReservationPeriodDayRateGenerator.CountHotelNights(fromDate, toDate);
                }

                await RegenerateDayRatesForPeriodAsync(
                    reservation,
                    targetUnit,
                    newPeriod,
                    periods.Where(p => string.Equals(p.Status, ReservationPeriodStatus.Closed, StringComparison.OrdinalIgnoreCase)).ToList(),
                    cancellationToken);

                await SyncAllUnitsFinancialsFromDayRatesAsync(reservation, units, cancellationToken);
                await RollUpReservationFinancialsAsync(reservation, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                await LogRentalPeriodActivityAsync(
                    ReservationActivityEvents.RentalPeriodAppended,
                    reservation,
                    newPeriod,
                    cancellationToken);

                var detail = await _reservationDetailService.GetByZaaerOrReservationIdAsync(
                    reservation.ZaaerId ?? reservation.ReservationId,
                    reservation.HotelId,
                    cancellationToken);

                return new ReservationPeriodAppendResultDto
                {
                    Period = MapPeriod(newPeriod),
                    Reservation = detail
                };
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<ReservationPeriodAppendResultDto?> UpdateActivePeriodAsync(
            int routeId,
            int periodId,
            ReservationPeriodUpdateRequestDto request,
            int? hotelId,
            CancellationToken cancellationToken = default)
        {
            await _permissionGuard.EnsureAsync("reservations.rental_periods", cancellationToken);

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (periodId <= 0)
            {
                throw new ArgumentException("PeriodId is required.");
            }

            var reservation = await FindReservationTrackedAsync(routeId, hotelId, cancellationToken);
            if (reservation == null)
            {
                return null;
            }

            var resRefs = ReservationPeriodStorage.GetReservationStorageRefs(reservation);
            var period = await _context.ReservationPeriods
                .FirstOrDefaultAsync(
                    p => p.PeriodId == periodId && resRefs.Contains(p.ReservationId),
                    cancellationToken);

            if (period == null)
            {
                throw new InvalidOperationException("reservationDetail.periods.periodNotFound");
            }

            if (!string.Equals(period.Status, ReservationPeriodStatus.Active, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("reservationDetail.periods.cannotEditClosed");
            }

            var units = await LoadReservationUnitsAsync(reservation, cancellationToken);
            if (units.Count == 0)
            {
                throw new InvalidOperationException("reservationDetail.periods.noUnits");
            }

            ReservationUnit targetUnit;
            if (period.UnitId.HasValue && period.UnitId.Value > 0)
            {
                targetUnit = await ResolveUnitByRouteOrStorageIdAsync(
                          units,
                          reservation,
                          period.UnitId.Value,
                          cancellationToken)
                    ?? units[0];
            }
            else
            {
                targetUnit = units[0];
            }

            var targetApartment = await LoadApartmentForUnitAsync(reservation, targetUnit, cancellationToken);

            var rentalNorm = !string.IsNullOrWhiteSpace(request.RentalType)
                ? ReservationPeriodDayRateGenerator.NormalizeRentalType(request.RentalType)
                : ReservationPeriodDayRateGenerator.NormalizeRentalType(period.RentalType);

            if (string.IsNullOrWhiteSpace(rentalNorm))
            {
                throw new ArgumentException("RentalType is required.");
            }

            var toDate = request.ToDate?.Date ?? period.ToDate.Date;
            var fromDate = period.FromDate.Date;

            if (toDate <= fromDate)
            {
                throw new InvalidOperationException("reservationDetail.periods.invalidDateRange");
            }

            var gross = request.GrossRate ?? period.GrossRate;
            if (gross <= 0m)
            {
                gross = await ResolveDefaultGrossForPeriodAsync(
                    reservation,
                    targetUnit,
                    rentalNorm,
                    fromDate,
                    cancellationToken);
            }

            if (gross <= 0m)
            {
                throw new InvalidOperationException("reservationDetail.periods.noGrossRate");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var periods = await _context.ReservationPeriods
                    .Where(p => resRefs.Contains(p.ReservationId))
                    .OrderBy(p => p.FromDate)
                    .ToListAsync(cancellationToken);

                period.RentalType = rentalNorm;
                period.ToDate = toDate;
                period.GrossRate = Math.Round(gross, 2, MidpointRounding.AwayFromZero);
                period.UpdatedAt = KsaTime.Now;

                var closedPeriods = periods
                    .Where(p =>
                        p.PeriodId != period.PeriodId &&
                        string.Equals(p.Status, ReservationPeriodStatus.Closed, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                await RemoveDayRatesOutsideActivePeriodAsync(
                    reservation,
                    targetUnit,
                    period,
                    closedPeriods,
                    units.Count,
                    cancellationToken);

                await RegenerateDayRatesForPeriodAsync(
                    reservation,
                    targetUnit,
                    period,
                    closedPeriods,
                    cancellationToken);

                ApplyCheckoutExtension(reservation, targetUnit, toDate);

                reservation.RentalType = rentalNorm;
                if (ReservationPeriodDayRateGenerator.IsMonthlyRental(rentalNorm))
                {
                    reservation.NumberOfMonths = Math.Max(1, reservation.NumberOfMonths ?? 1);
                    reservation.TotalNights = null;
                }
                else
                {
                    reservation.NumberOfMonths = null;
                    reservation.TotalNights = ReservationPeriodDayRateGenerator.CountHotelNights(fromDate, toDate);
                }

                await SyncAllUnitsFinancialsFromDayRatesAsync(reservation, units, cancellationToken);
                await RollUpReservationFinancialsAsync(reservation, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                await LogRentalPeriodActivityAsync(
                    ReservationActivityEvents.RentalPeriodUpdated,
                    reservation,
                    period,
                    cancellationToken);

                var detail = await _reservationDetailService.GetByZaaerOrReservationIdAsync(
                    reservation.ZaaerId ?? reservation.ReservationId,
                    reservation.HotelId,
                    cancellationToken);

                return new ReservationPeriodAppendResultDto
                {
                    Period = MapPeriod(period),
                    Reservation = detail
                };
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        private async Task LogRentalPeriodActivityAsync(
            string eventKey,
            Reservation reservation,
            ReservationPeriod period,
            CancellationToken cancellationToken)
        {
            await _activityLog.LogAsync(
                new ReservationActivityLogEntry
                {
                    EventKey = eventKey,
                    HotelId = reservation.HotelId,
                    ReservationId = reservation.ReservationId,
                    ReservationNo = reservation.ReservationNo,
                    RefType = "ReservationPeriod",
                    RefId = period.PeriodId,
                    AmountTo = period.GrossRate,
                    IconKey = eventKey == ReservationActivityEvents.RentalPeriodAppended ? "plus" : "edit",
                    Payload = new Dictionary<string, object?>
                    {
                        ["reservationNo"] = reservation.ReservationNo,
                        ["rentalType"] = period.RentalType,
                        ["fromDate"] = period.FromDate.ToString("yyyy-MM-dd"),
                        ["toDate"] = period.ToDate.ToString("yyyy-MM-dd"),
                        ["grossRate"] = period.GrossRate,
                        ["periodId"] = period.PeriodId
                    },
                    ZaaerId = reservation.ZaaerId
                },
                cancellationToken);
        }

        private async Task RemoveDayRatesOutsideActivePeriodAsync(
            Reservation reservation,
            ReservationUnit unit,
            ReservationPeriod period,
            IReadOnlyList<ReservationPeriod> closedPeriods,
            int reservationUnitCount,
            CancellationToken cancellationToken)
        {
            var protectedNights = ReservationPeriodDayRateGenerator.CollectProtectedNightDates(closedPeriods);
            var validNights = new HashSet<DateTime>(
                ReservationPeriodDayRateGenerator
                    .EnumerateNightDates(period.FromDate, period.ToDate, period.RentalType)
                    .Select(d => d.Date));

            var refs = GetDayRateReservationIdRefs(reservation);
            var apartment = await LoadApartmentForUnitAsync(reservation, unit, cancellationToken);
            var unitRefs = GetDayRateUnitIdRefs(unit, apartment);

            var allRows = await _context.ReservationUnitDayRates
                .Where(r => refs.Contains(r.ReservationId))
                .ToListAsync(cancellationToken);

            var rows = allRows.Where(r => unitRefs.Contains(r.UnitId)).ToList();
            if (reservationUnitCount == 1 && allRows.Count > 0 && rows.Count < allRows.Count)
            {
                rows = allRows;
            }

            foreach (var row in rows)
            {
                if (row.NightDate.Date < period.FromDate.Date)
                {
                    continue;
                }

                if (protectedNights.Contains(row.NightDate.Date))
                {
                    continue;
                }

                if (!validNights.Contains(row.NightDate.Date))
                {
                    _context.ReservationUnitDayRates.Remove(row);
                }
            }
        }

        private async Task SeedInitialPeriodInTransactionAsync(
            Reservation reservation,
            ReservationUnit unit,
            Apartment? apartment,
            List<ReservationPeriod> periods,
            CancellationToken cancellationToken)
        {
            var checkIn = (reservation.CheckInDate ?? unit.CheckInDate).Date;
            var checkOut = (reservation.CheckOutDate ?? unit.CheckOutDate).Date;
            var gross = await ResolveInitialPeriodGrossAsync(reservation, unit, cancellationToken);
            var today = KsaTime.Now.Date;

            var seed = new ReservationPeriod
            {
                ReservationId = ReservationPeriodStorage.GetStorageReservationId(reservation),
                UnitId = ReservationPeriodStorage.GetStorageUnitId(unit, apartment),
                RentalType = ReservationPeriodDayRateGenerator.NormalizeRentalType(reservation.RentalType),
                FromDate = checkIn,
                ToDate = checkOut,
                GrossRate = gross,
                TaxIncluded = true,
                Status = checkOut <= today ? ReservationPeriodStatus.Closed : ReservationPeriodStatus.Active,
                CreatedAt = KsaTime.Now
            };

            _context.ReservationPeriods.Add(seed);
            await _context.SaveChangesAsync(cancellationToken);
            periods.Add(seed);
        }

        private static DateTime ResolveSegmentToDate(ReservationPeriodAppendRequestDto request)
        {
            var raw = request.ToDate ?? request.NewCheckOutDate
                ?? throw new InvalidOperationException("reservationDetail.periods.toDateRequired");

            return raw.Date;
        }

        private static void ApplyCheckoutExtension(Reservation reservation, ReservationUnit unit, DateTime toDate)
        {
            var checkoutTime = reservation.CheckOutDate?.TimeOfDay ?? unit.CheckOutDate.TimeOfDay;
            if (checkoutTime == TimeSpan.Zero)
            {
                checkoutTime = new TimeSpan(18, 0, 0);
            }

            var combined = toDate.Date.Add(checkoutTime);
            reservation.CheckOutDate = combined;
            unit.CheckOutDate = combined;
            unit.DepartureDate = combined;
        }

        private async Task<decimal> ResolveInitialPeriodGrossAsync(
            Reservation reservation,
            ReservationUnit unit,
            CancellationToken cancellationToken)
        {
            if (unit.TotalAmount > 0m)
            {
                return unit.TotalAmount;
            }

            var refs = GetDayRateReservationIdRefs(reservation);
            var dayRate = await _context.ReservationUnitDayRates.AsNoTracking()
                .Where(r => refs.Contains(r.ReservationId))
                .OrderBy(r => r.NightDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (dayRate != null && dayRate.GrossRate > 0m)
            {
                return dayRate.GrossRate;
            }

            return reservation.TotalAmount ?? 0m;
        }

        private async Task<decimal> ResolveDefaultGrossForPeriodAsync(
            Reservation reservation,
            ReservationUnit unit,
            string rentalNorm,
            DateTime fromDate,
            CancellationToken cancellationToken)
        {
            var apartment = await _context.Apartments.AsNoTracking()
                .FirstOrDefaultAsync(
                    a =>
                        a.HotelId == reservation.HotelId &&
                        (a.ApartmentId == unit.ApartmentId || a.ZaaerId == unit.ApartmentId),
                    cancellationToken);

            if (apartment == null)
            {
                return 0m;
            }

            var (rateKeys, internalRoomTypeId) = await RoomTypeRateQueryHelper.BuildRateKeysForApartmentAsync(
                _context,
                apartment,
                reservation.HotelId,
                cancellationToken);

            if (rateKeys.Count == 0)
            {
                return 0m;
            }

            var rates = await _context.RoomTypeRates.AsNoTracking()
                .Where(r => r.HotelId == reservation.HotelId)
                .ToListAsync(cancellationToken);
            var baseRate = rates.FirstOrDefault(r => RoomTypeRateResolver.RateMatchesRoomType(r, rateKeys));
            RoomType? roomType = null;
            if (internalRoomTypeId.HasValue)
            {
                roomType = await _context.RoomTypes.AsNoTracking()
                    .FirstOrDefaultAsync(rt => rt.RoomTypeId == internalRoomTypeId.Value, cancellationToken);
            }

            var (gross, _) = await RoomTypeGrossRateResolver.ResolveAsync(
                _context,
                reservation.HotelId,
                null,
                rateKeys,
                baseRate,
                roomType,
                rentalNorm,
                fromDate,
                RoomTypeGrossRateOptions.Standard,
                cancellationToken);

            return gross;
        }

        private async Task RegenerateDayRatesForPeriodAsync(
            Reservation reservation,
            ReservationUnit unit,
            ReservationPeriod period,
            IReadOnlyList<ReservationPeriod> closedPeriods,
            CancellationToken cancellationToken)
        {
            var taxConfig = await HotelPricingTaxHelper.GetConfigAsync(_context, reservation.HotelId, cancellationToken);
            var storageReservationId = GetDayRateStorageReservationId(reservation);
            var apartment = await _context.Apartments.AsNoTracking()
                .FirstOrDefaultAsync(
                    a =>
                        a.HotelId == reservation.HotelId &&
                        (a.ApartmentId == unit.ApartmentId || a.ZaaerId == unit.ApartmentId),
                    cancellationToken);
            var storageUnitId = GetDayRateStorageUnitId(unit, apartment);

            var protectedNights = ReservationPeriodDayRateGenerator.CollectProtectedNightDates(closedPeriods);
            var nightDates = ReservationPeriodDayRateGenerator.EnumerateNightDates(
                period.FromDate,
                period.ToDate,
                period.RentalType);

            var perNightGross = period.GrossRate;
            var refs = GetDayRateReservationIdRefs(reservation);
            var unitRefs = GetDayRateUnitIdRefs(unit, apartment);

            var existingRows = await _context.ReservationUnitDayRates
                .Where(r => refs.Contains(r.ReservationId) && unitRefs.Contains(r.UnitId))
                .ToListAsync(cancellationToken);

            foreach (var night in nightDates)
            {
                if (protectedNights.Contains(night.Date))
                {
                    continue;
                }

                var calc = HotelPricingTaxHelper.CalculateAmounts(perNightGross, taxConfig);
                var existing = existingRows.FirstOrDefault(r => r.NightDate.Date == night.Date);
                if (existing == null)
                {
                    existing = new ReservationUnitDayRate
                    {
                        ReservationId = storageReservationId,
                        UnitId = storageUnitId,
                        NightDate = night.Date,
                        CreatedAt = KsaTime.Now
                    };
                    _context.ReservationUnitDayRates.Add(existing);
                    existingRows.Add(existing);
                }

                existing.GrossRate = perNightGross;
                existing.NetAmount = calc.NetAmount;
                existing.EwaAmount = calc.EwaAmount;
                existing.VatAmount = calc.VatAmount;
                existing.IsManual = false;
                existing.UpdatedAt = KsaTime.Now;
            }

            await ApplyDayRatesToUnitAndHeaderAsync(
                reservation,
                unit,
                apartment,
                reservationUnitCount: 1,
                cancellationToken);
        }

        private async Task ApplyDayRatesToUnitAndHeaderAsync(
            Reservation reservation,
            ReservationUnit unit,
            Apartment? apartment,
            int reservationUnitCount,
            CancellationToken cancellationToken)
        {
            var taxConfig = await HotelPricingTaxHelper.GetConfigAsync(_context, reservation.HotelId, cancellationToken);
            var refs = GetDayRateReservationIdRefs(reservation);
            var unitRefs = GetDayRateUnitIdRefs(unit, apartment);

            var allRows = await _context.ReservationUnitDayRates
                .Where(r => refs.Contains(r.ReservationId))
                .ToListAsync(cancellationToken);

            var rows = allRows.Where(r => unitRefs.Contains(r.UnitId)).ToList();
            // Single-unit stays may have legacy day-rate rows keyed with a different integration id.
            if (reservationUnitCount == 1 && allRows.Count > 0 && rows.Count < allRows.Count)
            {
                rows = allRows;
            }

            decimal net = 0m;
            decimal ewa = 0m;
            decimal vat = 0m;
            decimal total = 0m;
            foreach (var row in rows)
            {
                var calc = HotelPricingTaxHelper.CalculateAmounts(row.GrossRate, taxConfig);
                net += calc.NetAmount;
                ewa += calc.EwaAmount;
                vat += calc.VatAmount;
                total += calc.Total;
            }

            unit.RentAmount = Math.Round(net, 2, MidpointRounding.AwayFromZero);
            unit.VatRate = taxConfig.VatRate;
            unit.LodgingTaxRate = taxConfig.EwaRate;
            unit.VatAmount = Math.Round(vat, 2, MidpointRounding.AwayFromZero);
            unit.LodgingTaxAmount = Math.Round(ewa, 2, MidpointRounding.AwayFromZero);
            unit.TotalAmount = Math.Round(total, 2, MidpointRounding.AwayFromZero);
            unit.NumberOfNights = rows.Count > 0 ? rows.Count : unit.NumberOfNights;
        }

        private async Task SyncAllUnitsFinancialsFromDayRatesAsync(
            Reservation reservation,
            IReadOnlyList<ReservationUnit> units,
            CancellationToken cancellationToken)
        {
            foreach (var unit in units)
            {
                var apartment = await LoadApartmentForUnitAsync(reservation, unit, cancellationToken);
                await ApplyDayRatesToUnitAndHeaderAsync(
                    reservation,
                    unit,
                    apartment,
                    units.Count,
                    cancellationToken);
            }
        }

        private async Task RollUpReservationFinancialsAsync(Reservation reservation, CancellationToken cancellationToken)
        {
            var dayRateRefs = GetDayRateReservationIdRefs(reservation);
            var allDayRates = await _context.ReservationUnitDayRates
                .AsNoTracking()
                .Where(r => dayRateRefs.Contains(r.ReservationId) && r.GrossRate > 0m)
                .ToListAsync(cancellationToken);

            var refs = GetReservationRateRefs(reservation);
            var units = await _context.ReservationUnits
                .Where(u => refs.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);

            var taxConfig = await HotelPricingTaxHelper.GetConfigAsync(_context, reservation.HotelId, cancellationToken);

            decimal subtotal = 0m;
            decimal ewa = 0m;
            decimal vat = 0m;
            decimal rentTotal = 0m;

            if (allDayRates.Count > 0)
            {
                foreach (var row in allDayRates)
                {
                    var calc = HotelPricingTaxHelper.CalculateAmounts(row.GrossRate, taxConfig);
                    subtotal += calc.NetAmount;
                    ewa += calc.EwaAmount;
                    vat += calc.VatAmount;
                    rentTotal += calc.Total;
                }
            }
            else
            {
                foreach (var u in units)
                {
                    subtotal += u.RentAmount;
                    ewa += u.LodgingTaxAmount ?? 0m;
                    vat += u.VatAmount ?? 0m;
                    rentTotal += u.TotalAmount;
                }
            }

            var extraTotal = await _context.ReservationExtras
                .Where(e => refs.Contains(e.ReservationId))
                .SumAsync(e => (decimal?)e.TotalAmount, cancellationToken) ?? 0m;

            var penalties = await _context.Penalties
                .Where(p => refs.Contains(p.ReservationId) && p.IsActive)
                .SumAsync(p => (decimal?)p.TotalAmount, cancellationToken) ?? 0m;

            var discounts = await _context.Discounts
                .Where(d => refs.Contains(d.ReservationId) && d.IsActive)
                .SumAsync(d => (decimal?)d.DiscountAmount, cancellationToken) ?? 0m;

            reservation.Subtotal = Math.Round(subtotal, 2, MidpointRounding.AwayFromZero);
            reservation.LodgingTaxAmount = Math.Round(ewa, 2, MidpointRounding.AwayFromZero);
            reservation.VatAmount = Math.Round(vat, 2, MidpointRounding.AwayFromZero);
            reservation.TotalTaxAmount = Math.Round(ewa + vat, 2, MidpointRounding.AwayFromZero);
            reservation.VatRate = taxConfig.VatRate;
            reservation.LodgingTaxRate = taxConfig.EwaRate;
            reservation.TotalExtra = Math.Round(extraTotal, 2, MidpointRounding.AwayFromZero);
            reservation.TotalPenalties = Math.Round(penalties, 2, MidpointRounding.AwayFromZero);
            reservation.TotalDiscounts = Math.Round(discounts, 2, MidpointRounding.AwayFromZero);
            reservation.TotalAmount = Math.Round(rentTotal + extraTotal + penalties, 2, MidpointRounding.AwayFromZero);
            reservation.AmountPaid ??= 0m;
            reservation.BalanceAmount = Math.Round(
                reservation.TotalAmount.GetValueOrDefault() - (reservation.TotalDiscounts ?? 0m) - reservation.AmountPaid.GetValueOrDefault(),
                2,
                MidpointRounding.AwayFromZero);
        }

        private Task<ReservationPeriodListResponseDto> BuildListResponseAsync(
            Reservation reservation,
            CancellationToken cancellationToken) =>
            ReservationPeriodQueries.BuildListAsync(_context, reservation, cancellationToken);

        private async Task<Apartment?> LoadApartmentForUnitAsync(
            Reservation reservation,
            ReservationUnit unit,
            CancellationToken cancellationToken) =>
            await _context.Apartments.AsNoTracking()
                .FirstOrDefaultAsync(
                    a =>
                        a.HotelId == reservation.HotelId &&
                        (a.ApartmentId == unit.ApartmentId || a.ZaaerId == unit.ApartmentId),
                    cancellationToken);

        private async Task<ReservationUnit?> ResolveUnitByRouteOrStorageIdAsync(
            IReadOnlyList<ReservationUnit> units,
            Reservation reservation,
            int routeOrStorageUnitId,
            CancellationToken cancellationToken)
        {
            var direct = units.FirstOrDefault(u => u.UnitId == routeOrStorageUnitId);
            if (direct != null)
            {
                return direct;
            }

            foreach (var unit in units)
            {
                var apartment = await LoadApartmentForUnitAsync(reservation, unit, cancellationToken);
                if (ReservationPeriodStorage.GetUnitStorageRefs(unit, apartment).Contains(routeOrStorageUnitId))
                {
                    return unit;
                }
            }

            return null;
        }

        private static ReservationPeriodDto MapPeriod(ReservationPeriod p) =>
            ReservationPeriodQueries.Map(p);

        private async Task<Reservation?> FindReservationAsync(
            int id,
            int? hotelId,
            CancellationToken cancellationToken)
        {
            return await _context.Reservations.AsNoTracking()
                .Where(r =>
                    (r.ZaaerId == id || r.ReservationId == id) &&
                    (!hotelId.HasValue || r.HotelId == hotelId.Value))
                .OrderByDescending(r => r.ZaaerId == id ? 1 : 0)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private Task<Reservation?> FindReservationTrackedAsync(
            int id,
            int? hotelId,
            CancellationToken cancellationToken)
        {
            return _context.Reservations
                .Where(r =>
                    (r.ZaaerId == id || r.ReservationId == id) &&
                    (!hotelId.HasValue || r.HotelId == hotelId.Value))
                .OrderByDescending(r => r.ZaaerId == id ? 1 : 0)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private async Task<List<ReservationUnit>> LoadReservationUnitsAsync(
            Reservation reservation,
            CancellationToken cancellationToken)
        {
            var refs = GetReservationRateRefs(reservation);
            return await _context.ReservationUnits
                .Where(u => refs.Contains(u.ReservationId))
                .OrderBy(u => u.CheckInDate)
                .ToListAsync(cancellationToken);
        }

        private static IReadOnlyList<int> GetReservationRateRefs(Reservation reservation)
        {
            var refs = new List<int> { reservation.ReservationId };
            if (reservation.ZaaerId.HasValue && reservation.ZaaerId.Value != reservation.ReservationId)
            {
                refs.Add(reservation.ZaaerId.Value);
            }

            return refs;
        }

        private static int GetDayRateStorageReservationId(Reservation reservation) =>
            reservation.ZaaerId is > 0 ? reservation.ZaaerId.Value : reservation.ReservationId;

        private static int GetDayRateStorageUnitId(ReservationUnit unit, Apartment? apartment)
        {
            if (apartment != null)
            {
                return apartment.ZaaerId is > 0 ? apartment.ZaaerId.Value : apartment.ApartmentId;
            }

            return unit.ApartmentId;
        }

        private static IReadOnlyList<int> GetDayRateReservationIdRefs(Reservation reservation)
        {
            var refs = new HashSet<int>(GetReservationRateRefs(reservation))
            {
                GetDayRateStorageReservationId(reservation)
            };

            return refs.ToList();
        }

        private static IReadOnlyList<int> GetDayRateUnitIdRefs(ReservationUnit unit, Apartment? apartment)
        {
            var refs = new HashSet<int>
            {
                GetDayRateStorageUnitId(unit, apartment),
                unit.ApartmentId,
                unit.UnitId
            };

            if (unit.ZaaerId is > 0)
            {
                refs.Add(unit.ZaaerId.Value);
            }

            if (apartment != null)
            {
                refs.Add(apartment.ApartmentId);
                if (apartment.ZaaerId is > 0)
                {
                    refs.Add(apartment.ZaaerId.Value);
                }
            }

            return refs.Where(x => x > 0).ToList();
        }
    }
}
