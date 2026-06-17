#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.Pms.ReservationDetail
{
    /// <summary>
    /// One reservation unit line from the PMS editor (create or update on PATCH).
    /// </summary>
    public sealed class ReservationPmsUnitPatchDto
    {
        /// <summary>Existing <c>reservation_units.unit_id</c>. Omit, null, or &lt;= 0 for a new line (apartment must be set).</summary>
        public int? UnitId { get; init; }

        /// <summary>Internal <c>apartments.apartment_id</c> from picker.</summary>
        public int? ApartmentId { get; init; }

        /// <summary><c>apartments.zaaer_id</c> when known.</summary>
        public int? ApartmentZaaerId { get; init; }

        public DateTime? CheckInDate { get; init; }

        public DateTime? CheckOutDate { get; init; }

        public DateTime? DepartureDate { get; init; }
    }
}
