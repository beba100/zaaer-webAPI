#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.Pms.ReservationDetail
{
    /// <summary>
    /// Partial update from PMS reservation editor (mapped to <see cref="FinanceLedgerAPI.Models.Reservation"/>).
    /// </summary>
    public class ReservationPmsPatchDto
    {
        /// <summary>individual | company</summary>
        public string? ReservationKind { get; init; }

        /// <summary>PMS values: <c>confirmed</c>, <c>unconfirmed</c>, <c>checked_in</c>, <c>checked_out</c>, <c>no_show</c>, <c>cancelled</c>; legacy enum names and <c>check_in</c> accepted.</summary>
        public string? ReservationStatus { get; init; }

        public int? VisitPurposeId { get; init; }

        public string? Source { get; init; }

        /// <summary>Optional external CM / channel booking reference (alphanumeric).</summary>
        public string? CmBookingNo { get; init; }

        /// <summary>Daily, Monthly, Yearly, InHour (matches <c>RentalType</c> enum names).</summary>
        public string? RentalType { get; init; }

        public DateTime? CheckInDate { get; init; }

        public DateTime? CheckOutDate { get; init; }

        public int? NumberOfMonths { get; init; }

        public int? TotalNights { get; init; }

        /// <summary>ThirtyDay | Actual — monthly rental checkout calculation.</summary>
        public string? MonthlyCalendarMode { get; init; }

        public bool? IsAutoExtend { get; init; }

        public int? CorporateId { get; init; }

        public int? CustomerId { get; init; }

        /// <summary>
        /// When set (including empty list), replaces rows in <c>dbo.reservation_companions</c> for this reservation.
        /// Omit the property to leave existing JSON unchanged.
        /// </summary>
        public List<ReservationDetailCompanionDto>? Companions { get; init; }

        /// <summary>
        /// When set, replaces <c>reservation_units</c> for this reservation to match the editor (add/remove/update lines).
        /// Omit to leave units unchanged (e.g. guest-only PATCH).
        /// </summary>
        public List<ReservationPmsUnitPatchDto>? Units { get; init; }

        /// <summary>
        /// When set, replaces all rows in <c>dbo.reservation_extras</c> for this reservation (same transaction as other patch fields).
        /// </summary>
        public List<ReservationPmsExtraPatchDto>? Extras { get; init; }
    }
}
