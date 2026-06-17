#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.Pms.ReservationDetail
{
    /// <summary>
    /// One companion line persisted in <c>dbo.reservation_companions</c> and returned on reservation detail GET/PATCH.
    /// </summary>
    public sealed class ReservationDetailCompanionDto
    {
        public int? RowKey { get; init; }

        public int CustomerId { get; init; }

        public int? CustomerZaaerId { get; init; }

        public string CustomerName { get; init; } = string.Empty;

        public string? IdTypeName { get; init; }

        public string? IdTypeNameAr { get; init; }

        public string? IdNumber { get; init; }

        public DateTime? BirthDate { get; init; }

        public string? NationalityName { get; init; }

        public string? NationalityNameAr { get; init; }

        public string? MobileNo { get; init; }

        public string? Email { get; init; }

        /// <summary>
        /// <c>reservation_units.unit_id</c> for the PMS grid; persisted companion row uses apartment zaaer / id in <c>unit_id</c>.
        /// </summary>
        public int? UnitId { get; init; }

        /// <summary>Optional apartment zaaer for the chosen unit line (helps resolve when <see cref="UnitId"/> is ambiguous).</summary>
        public int? ApartmentZaaerId { get; init; }

        /// <summary>FK to <c>customer_relations.cr_id</c>.</summary>
        public int? RelationId { get; init; }
    }
}
