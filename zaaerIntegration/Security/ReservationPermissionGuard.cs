using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms.ReservationDetail;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;
using FinanceLedgerAPI.Models;

namespace zaaerIntegration.Security
{
    /// <summary>Business-level permission checks for reservation detail operations (beyond action-level <see cref="RequirePermissionAttribute"/>).</summary>
    public sealed class ReservationPermissionGuard
    {
        private const string StayDatesAfterCheckinPermission = "reservations.edit_stay_dates_after_checkin";

        private readonly ICurrentUserContext _user;
        private readonly IPermissionService _permissions;
        private readonly ApplicationDbContext _context;

        public ReservationPermissionGuard(
            ICurrentUserContext user,
            IPermissionService permissions,
            ApplicationDbContext context)
        {
            _user = user;
            _permissions = permissions;
            _context = context;
        }

        public void Ensure(string permissionCode)
        {
            if (Has(permissionCode))
            {
                return;
            }

            throw new ReservationPermissionDeniedException(permissionCode);
        }

        public async Task EnsureAsync(string permissionCode, CancellationToken cancellationToken = default)
        {
            if (await HasAsync(permissionCode, cancellationToken))
            {
                return;
            }

            throw new ReservationPermissionDeniedException(permissionCode);
        }

        public bool Has(string permissionCode)
        {
            if (!_user.IsAuthenticated || !_user.UserId.HasValue || !_user.TenantId.HasValue)
            {
                return false;
            }

            return _permissions
                .HasPermissionAsync(
                    _user.UserId.Value,
                    _user.TenantId.Value,
                    permissionCode,
                    _user.AuthMode,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }

        public async Task<bool> HasAsync(string permissionCode, CancellationToken cancellationToken = default)
        {
            if (!_user.IsAuthenticated || !_user.UserId.HasValue || !_user.TenantId.HasValue)
            {
                return false;
            }

            return await _permissions.HasPermissionAsync(
                _user.UserId.Value,
                _user.TenantId.Value,
                permissionCode,
                _user.AuthMode,
                cancellationToken);
        }

        /// <summary>
        /// After check-in: <c>reservations.pricing_edit</c> or <c>reservations.pricing_edit_after_checkin</c>.
        /// Before check-in / new flow: <c>reservations.pricing_edit</c> or <c>reservations.update</c>.
        /// View-only (<c>reservations.pricing_view</c>) is not sufficient to save rates.
        /// </summary>
        private async Task EnsurePricingSaveAsync(Reservation reservation, CancellationToken cancellationToken)
        {
            if (await HasAsync("reservations.pricing_edit", cancellationToken))
            {
                return;
            }

            if (await IsReservationAfterCheckInForPricingAsync(reservation, cancellationToken))
            {
                if (await HasAsync("reservations.pricing_edit_after_checkin", cancellationToken))
                {
                    return;
                }

                throw new ReservationPermissionDeniedException("reservations.pricing_edit_after_checkin");
            }

            if (await HasAsync("reservations.update", cancellationToken))
            {
                return;
            }

            throw new ReservationPermissionDeniedException("reservations.update");
        }

        private async Task<bool> IsReservationAfterCheckInForPricingAsync(
            Reservation reservation,
            CancellationToken cancellationToken)
        {
            if (IsCheckedInReservation(reservation.Status))
            {
                return true;
            }

            var refs = GetReservationUnitRefs(reservation);
            // Status normalization cannot run inside IQueryable — EF cannot translate IsCheckedInUnitStatus.
            var unitStatuses = await _context.ReservationUnits.AsNoTracking()
                .Where(u => refs.Contains(u.ReservationId))
                .Select(u => u.Status)
                .ToListAsync(cancellationToken);

            return unitStatuses.Any(IsCheckedInUnitStatus);
        }

        /// <summary>
        /// Validates granular permissions only when the PATCH actually changes restricted fields.
        /// Routine save with <c>reservations.update</c> must not require every sub-permission when values are unchanged.
        /// </summary>
        public async Task ValidatePatchAsync(
            Reservation entity,
            ReservationPmsPatchDto patch,
            CancellationToken cancellationToken = default)
        {
            await ValidateRestrictedDateAndAutoExtendPatchAsync(entity, patch, cancellationToken);

            // Enterprise rule: reservations.update covers routine save — no unrelated sub-permission checks.
            if (await HasAsync("reservations.update", cancellationToken))
            {
                return;
            }

            if (IsNewReservationStaySetup(entity)
                && await HasAsync("reservations.create", cancellationToken))
            {
                return;
            }

            await EnsureGranularPatchWithoutBaseUpdateAsync(entity, patch, cancellationToken);
        }

        /// <summary>
        /// First-save setup: row exists in DB (<c>unconfirmed</c>) with shell dates from <c>CreateReservationAsync</c> until PATCH completes editor fields.
        /// Stay-date sub-permissions apply only after the reservation leaves this setup state (listed / operational).
        /// </summary>
        private static bool IsNewReservationStaySetup(Reservation entity)
        {
            if (IsCheckedInReservation(entity.Status))
            {
                return false;
            }

            return string.Equals(
                NormalizeReservationStatus(entity.Status),
                "unconfirmed",
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Listed reservations only: date/time rules apply even when <c>reservations.update</c> is held.
        /// New reservations (draft <c>unconfirmed</c> setup) use <c>create</c>/<c>update</c> only.
        /// </summary>
        private async Task ValidateRestrictedDateAndAutoExtendPatchAsync(
            Reservation entity,
            ReservationPmsPatchDto patch,
            CancellationToken cancellationToken)
        {
            if (IsNewReservationStaySetup(entity))
            {
                return;
            }

            var stayDatesChanged = false;

            if (patch.CheckInDate.HasValue
                && CheckInDateTimeChanged(entity.CheckInDate, patch.CheckInDate)
                && !IsInitialStayDateAssignment(entity.CheckInDate, patch.CheckInDate))
            {
                stayDatesChanged = true;
            }

            if (patch.CheckOutDate.HasValue
                && CheckOutDateTimeChanged(entity.CheckOutDate, patch.CheckOutDate)
                && !IsInitialStayDateAssignment(entity.CheckOutDate, patch.CheckOutDate))
            {
                stayDatesChanged = true;
            }

            if (!string.IsNullOrWhiteSpace(patch.RentalType))
            {
                var incoming = NormalizeRentalType(patch.RentalType);
                var current = NormalizeRentalType(entity.RentalType);
                if (!string.Equals(incoming, current, StringComparison.OrdinalIgnoreCase))
                {
                    stayDatesChanged = true;
                }
            }

            if (stayDatesChanged)
            {
                await EnsureAsync(StayDatesAfterCheckinPermission, cancellationToken);
            }

            if (!patch.IsAutoExtend.HasValue || patch.IsAutoExtend.Value == entity.IsAutoExtend)
            {
                return;
            }

            if (await HasAsync("reservations.auto_extend", cancellationToken))
            {
                if (!patch.IsAutoExtend.Value)
                {
                    throw new ReservationPermissionDeniedException("reservations.auto_extend");
                }

                return;
            }

            if (!await HasAsync("reservations.update", cancellationToken))
            {
                throw new ReservationPermissionDeniedException("reservations.update");
            }
        }

        /// <summary>
        /// When <c>reservations.update</c> is missing, allow PATCH only if changed fields are covered by granular grants.
        /// </summary>
        private async Task EnsureGranularPatchWithoutBaseUpdateAsync(
            Reservation entity,
            ReservationPmsPatchDto patch,
            CancellationToken cancellationToken)
        {
            if (HasGeneralPatchChanges(entity, patch))
            {
                throw new ReservationPermissionDeniedException("reservations.update");
            }

            var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!IsNewReservationStaySetup(entity)
                && !string.IsNullOrWhiteSpace(patch.RentalType))
            {
                var incoming = NormalizeRentalType(patch.RentalType);
                var current = NormalizeRentalType(entity.RentalType);
                if (!string.Equals(incoming, current, StringComparison.OrdinalIgnoreCase))
                {
                    required.Add(StayDatesAfterCheckinPermission);
                }
            }

            if (!string.IsNullOrWhiteSpace(patch.ReservationKind))
            {
                var incomingKind = NormalizeReservationKind(patch.ReservationKind);
                var currentKind = NormalizeReservationKind(entity.ReservationType);
                if (!string.Equals(incomingKind, currentKind, StringComparison.OrdinalIgnoreCase))
                {
                    required.Add("reservations.company_add");
                }
            }

            if (patch.CorporateId.HasValue && patch.CorporateId != entity.CorporateId)
            {
                required.Add("reservations.company_add");
            }

            if (!string.IsNullOrWhiteSpace(patch.ReservationStatus))
            {
                var incoming = NormalizeReservationStatus(patch.ReservationStatus);
                var current = NormalizeReservationStatus(entity.Status);
                if (!string.Equals(incoming, current, StringComparison.OrdinalIgnoreCase))
                {
                    if (incoming is "noshow")
                    {
                        required.Add("reservations.no_show");
                    }

                    if (incoming is "cancelled")
                    {
                        required.Add("reservations.cancel");
                    }

                    if (incoming is "checkedin")
                    {
                        required.Add("reservations.check_in");
                    }

                    if (incoming is "confirmed" && current is "checkedin")
                    {
                        required.Add("reservations.undo_check_in");
                    }
                }
            }

            if (patch.Units != null)
            {
                var refs = GetReservationUnitRefs(entity);
                var existing = await _context.ReservationUnits.AsNoTracking()
                    .Where(u => refs.Contains(u.ReservationId))
                    .Select(u => u.UnitId)
                    .ToListAsync(cancellationToken);

                var incomingIds = patch.Units
                    .Where(u => u.UnitId is > 0)
                    .Select(u => u.UnitId!.Value)
                    .ToHashSet();

                if (existing.Any(id => !incomingIds.Contains(id)))
                {
                    required.Add("reservations.unit_remove");
                }

                if (incomingIds.Any(id => !existing.Contains(id)))
                {
                    required.Add("reservations.unit_add");
                }
            }

            if (patch.Extras != null && patch.Extras.Count > 0)
            {
                required.Add("reservations.package");
            }

            if (required.Count == 0)
            {
                if (patch.Companions != null
                    && (await HasAsync("guests.create", cancellationToken)
                        || await HasAsync("guests.update", cancellationToken)))
                {
                    return;
                }

                throw new ReservationPermissionDeniedException("reservations.update");
            }

            if (patch.Companions != null
                && !await HasAsync("guests.create", cancellationToken)
                && !await HasAsync("guests.update", cancellationToken))
            {
                required.Add("guests.create");
            }

            foreach (var code in required)
            {
                await EnsureAsync(code, cancellationToken);
            }
        }

        private static bool HasGeneralPatchChanges(Reservation entity, ReservationPmsPatchDto patch)
        {
            if (patch.VisitPurposeId.HasValue && patch.VisitPurposeId != entity.VisitPurposeId)
            {
                return true;
            }

            if (patch.Source != null &&
                !string.Equals(patch.Source.Trim(), (entity.Source ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (patch.CmBookingNo != null)
            {
                var incoming = patch.CmBookingNo.Trim();
                var current = (entity.CmBookingNo ?? "").Trim();
                if (!string.Equals(incoming, current, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (patch.CustomerId.HasValue && patch.CustomerId != entity.CustomerId)
            {
                return true;
            }

            if (patch.CheckInDate.HasValue && !IsCheckedInReservation(entity.Status) &&
                CheckInDateTimeChanged(entity.CheckInDate, patch.CheckInDate))
            {
                return true;
            }

            if (patch.CheckOutDate.HasValue && !IsCheckedInReservation(entity.Status) &&
                CheckOutDateTimeChanged(entity.CheckOutDate, patch.CheckOutDate))
            {
                return true;
            }

            return false;
        }

        private static string NormalizeReservationKind(string? kind)
        {
            var k = (kind ?? "").Trim().ToLowerInvariant();
            if (k is "company" or "corporate" or "شركة" or "شركات")
            {
                return "corporate";
            }

            return "individual";
        }

        public async Task ValidateSaveUnitDayRatesAsync(
            Reservation reservation,
            ReservationUnitDayRatesSaveRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var refs = GetReservationUnitRefs(reservation);
            var reservationIdRefs = new HashSet<int>(refs)
            {
                reservation.ZaaerId is > 0 ? reservation.ZaaerId.Value : reservation.ReservationId
            };
            var existingRows = await _context.ReservationUnitDayRates.AsNoTracking()
                .Where(r => reservationIdRefs.Contains(r.ReservationId))
                .ToListAsync(cancellationToken);

            var units = await _context.ReservationUnits.AsNoTracking()
                .Where(u => refs.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);

            var unitById = units.ToDictionary(u => u.UnitId);
            var anyPriceChange = false;

            foreach (var item in request.Items ?? Array.Empty<ReservationUnitDayRateSaveDto>())
            {
                if (!unitById.TryGetValue(item.UnitId, out var unit))
                {
                    unit = units.FirstOrDefault(u => u.ApartmentId == item.UnitId || u.ZaaerId == item.UnitId);
                }

                var unitMatchIds = new HashSet<int> { item.UnitId };
                if (unit != null)
                {
                    unitMatchIds.Add(unit.UnitId);
                    if (unit.ZaaerId is > 0)
                    {
                        unitMatchIds.Add(unit.ZaaerId.Value);
                    }

                    unitMatchIds.Add(unit.ApartmentId);
                }

                var existing = item.RateId.HasValue
                    ? existingRows.FirstOrDefault(r => r.RateId == item.RateId.Value)
                    : existingRows.FirstOrDefault(r =>
                        unitMatchIds.Contains(r.UnitId) && r.NightDate.Date == item.NightDate.Date);

                if (existing == null || Math.Abs(existing.GrossRate - item.GrossRate) > 0.001m)
                {
                    anyPriceChange = true;
                }
            }

            if (!anyPriceChange)
            {
                return;
            }

            await EnsurePricingSaveAsync(reservation, cancellationToken);

            foreach (var item in request.Items ?? Array.Empty<ReservationUnitDayRateSaveDto>())
            {
                if (!unitById.TryGetValue(item.UnitId, out var unit))
                {
                    unit = units.FirstOrDefault(u => u.ApartmentId == item.UnitId || u.ZaaerId == item.UnitId);
                }

                if (!await HasAsync("reservations.pricing_below_minimum", cancellationToken))
                {
                    var minRate = await ResolveMinimumRateForUnitAsync(reservation, unit, cancellationToken);
                    if (minRate.HasValue && item.GrossRate < minRate.Value - 0.001m)
                    {
                        throw new ReservationPermissionDeniedException("reservations.pricing_below_minimum");
                    }
                }
            }
        }

        private async Task<decimal?> ResolveMinimumRateForUnitAsync(
            Reservation reservation,
            ReservationUnit? unit,
            CancellationToken cancellationToken)
        {
            if (unit == null)
            {
                return null;
            }

            var apt = await _context.Apartments.AsNoTracking()
                .FirstOrDefaultAsync(
                    a => a.HotelId == reservation.HotelId && (a.ApartmentId == unit.ApartmentId || a.ZaaerId == unit.ApartmentId),
                    cancellationToken);

            if (apt?.RoomTypeId == null)
            {
                return null;
            }

            var rtId = apt.RoomTypeId.Value;
            var rateRow = await _context.RoomTypeRates.AsNoTracking()
                .Where(r => r.HotelId == reservation.HotelId && r.RoomTypeId == rtId)
                .OrderBy(r => r.RateId)
                .FirstOrDefaultAsync(cancellationToken);

            if (rateRow == null)
            {
                return null;
            }

            var rental = (reservation.RentalType ?? "daily").Trim().ToLowerInvariant();
            if (rental.Contains("month"))
            {
                return rateRow.MonthlyRateMin ?? rateRow.MonthlyRate;
            }

            return rateRow.DailyRateMin ?? rateRow.DailyRateLowWeekdays ?? rateRow.DailyRateHighWeekdays;
        }

        private const double DateTimeEquivalenceMinutes = 2;

        private static DateTime ToSaudi(DateTime value) => KsaTime.ToSaudiTime(value);

        /// <summary>
        /// DB stay dates are saved as KSA wall clock (Unspecified). PATCH values arrive as UTC from JSON.
        /// </summary>
        private static DateTime NormalizeStoredStayDateTime(DateTime value) =>
            value.Kind == DateTimeKind.Unspecified ? value : ToSaudi(value);

        private static DateTime NormalizeIncomingStayDateTime(DateTime value) => ToSaudi(value);

        private static bool SaudiDateTimeEquivalent(DateTime? stored, DateTime? incoming)
        {
            if (!stored.HasValue && !incoming.HasValue)
            {
                return true;
            }

            if (!stored.HasValue || !incoming.HasValue)
            {
                return false;
            }

            var a = NormalizeStoredStayDateTime(stored.Value);
            var b = NormalizeIncomingStayDateTime(incoming.Value);
            return Math.Abs((a - b).TotalMinutes) < DateTimeEquivalenceMinutes;
        }

        private static bool CheckInDateTimeChanged(DateTime? stored, DateTime? incoming) =>
            !SaudiDateTimeEquivalent(stored, incoming);

        private static bool CheckOutDateTimeChanged(DateTime? stored, DateTime? incoming) =>
            !SaudiDateTimeEquivalent(stored, incoming);

        /// <summary>First time stay dates are written (e.g. new reservation after draft) — no date sub-permissions.</summary>
        private static bool IsInitialStayDateAssignment(DateTime? stored, DateTime? incoming) =>
            !stored.HasValue && incoming.HasValue;

        private static bool SaudiCalendarDateEqual(DateTime? stored, DateTime? incoming)
        {
            if (!stored.HasValue || !incoming.HasValue)
            {
                return false;
            }

            return ToSaudi(stored.Value).Date == ToSaudi(incoming.Value).Date;
        }

        private static bool IsCheckInTimeOnlyChange(DateTime? stored, DateTime? incoming) =>
            CheckInDateTimeChanged(stored, incoming) && SaudiCalendarDateEqual(stored, incoming);

        private static bool IsCheckOutTimeOnlyChange(DateTime? stored, DateTime? incoming) =>
            CheckOutDateTimeChanged(stored, incoming) && SaudiCalendarDateEqual(stored, incoming);

        private static bool IsSaudiArrivalMovedToPast(DateTime? stored, DateTime? incoming)
        {
            if (!stored.HasValue || !incoming.HasValue)
            {
                return false;
            }

            var oldDate = ToSaudi(stored.Value).Date;
            var newDate = ToSaudi(incoming.Value).Date;
            return newDate < oldDate;
        }

        private static bool IsSaudiDepartureMovedToPast(DateTime? stored, DateTime? incoming)
        {
            if (!stored.HasValue || !incoming.HasValue)
            {
                return false;
            }

            var oldDate = ToSaudi(stored.Value).Date;
            var newDate = ToSaudi(incoming.Value).Date;
            return newDate < oldDate;
        }

        private static string NormalizeRentalType(string? rentalType)
        {
            if (string.IsNullOrWhiteSpace(rentalType))
            {
                return "daily";
            }

            var v = rentalType.Trim().ToLowerInvariant();
            if (v.Contains("month"))
            {
                return "monthly";
            }

            if (v.Contains("year"))
            {
                return "yearly";
            }

            if (v.Contains("hour"))
            {
                return "hourly";
            }

            return "daily";
        }

        private static string NormalizeReservationStatus(string? status)
        {
            var s = (status ?? "").Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");
            return s switch
            {
                "checkin" or "checkedin" => "checkedin",
                "checkedout" or "checkout" => "checkedout",
                "noshow" => "noshow",
                "cancelled" or "canceled" => "cancelled",
                "confirmed" => "confirmed",
                "unconfirmed" => "unconfirmed",
                _ => s
            };
        }

        private static bool IsCheckedInReservation(string? status) =>
            NormalizeReservationStatus(status) == "checkedin";

        private static bool IsCheckedInUnitStatus(string? status)
        {
            var s = (status ?? "").Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "");
            return s is "checkedin" or "checked_in" or "checkin";
        }

        private static List<int> GetReservationUnitRefs(Reservation entity)
        {
            var refs = new List<int> { entity.ReservationId };
            if (entity.ZaaerId is > 0)
            {
                refs.Add(entity.ZaaerId.Value);
            }

            return refs;
        }
    }
}
