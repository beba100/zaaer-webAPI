#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.Pms.ReservationDetail
{
    /// <summary>
    /// One extra line sent from the PMS reservation editor; replaces rows in <c>dbo.reservation_extras</c> when the parent patch includes <c>Extras</c>.
    /// </summary>
    public sealed class ReservationPmsExtraPatchDto
    {
        /// <summary>Optional reservation unit line id (<c>reservation_units.unit_id</c>) from the editor room dropdown.</summary>
        public int? ReservationUnitId { get; init; }

        public int? PackageId { get; init; }

        public string? ItemName { get; init; }

        /// <summary>e.g. OnCheckIn, Daily, PerStay, OnCheckOut, OnCustomDate.</summary>
        public string? PostingRule { get; init; }

        public DateTime? ServiceDate { get; init; }

        public int? GuestCount { get; init; }

        public int? NightCount { get; init; }

        /// <summary>Override catalog price when set.</summary>
        public decimal? UnitPrice { get; init; }
    }
}
