#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.Pms.ReservationDetail
{
    /// <summary>
    /// One extra / add-on line from <c>dbo.reservation_extras</c> (GET today; PATCH later).
    /// </summary>
    public sealed class ReservationDetailExtraDto
    {
        public int ExtraId { get; init; }

        public int? UnitId { get; init; }

        /// <summary>Resolved room label when <see cref="UnitId"/> is set.</summary>
        public string? RoomLabel { get; init; }

        public int? PackageId { get; init; }

        public string? ItemName { get; init; }

        public string PostingRule { get; init; } = "OnCheckIn";

        public DateTime? ServiceDate { get; init; }

        public int? GuestCount { get; init; }

        public int? NightCount { get; init; }

        public decimal UnitPrice { get; init; }

        public decimal Subtotal { get; init; }

        public decimal TaxAmount { get; init; }

        public decimal TotalAmount { get; init; }

        public int? CreatedBy { get; init; }

        public DateTime? CreatedAt { get; init; }
    }
}
