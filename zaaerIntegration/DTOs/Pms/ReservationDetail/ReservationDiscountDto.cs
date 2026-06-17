#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.Pms.ReservationDetail
{
    public sealed class CreateReservationDiscountDto
    {
        /// <summary>Route id: internal <c>reservation_id</c> or <c>zaaer_id</c>.</summary>
        public int ReservationId { get; init; }

        public int? HotelId { get; init; }

        /// <summary><c>reservation</c> (full stay) or <c>selectedUnits</c> (unit rent only).</summary>
        public string ApplyScope { get; init; } = "reservation";

        /// <summary><c>Amount</c> or <c>Percentage</c>.</summary>
        public string CalculationMethod { get; init; } = "Amount";

        public decimal CalculationValue { get; init; }

        public string? Description { get; init; }

        /// <summary>
        /// When <see cref="ApplyScope"/> is <c>selectedUnits</c>, UI unit line ids
        /// (<c>reservation_units.unit_id</c> from the detail API).
        /// </summary>
        public IReadOnlyList<int>? UnitIds { get; init; }
    }

    public sealed class UpdateReservationDiscountDto
    {
        public int ReservationId { get; init; }

        public int? HotelId { get; init; }

        public string ApplyScope { get; init; } = "reservation";

        public string CalculationMethod { get; init; } = "Amount";

        public decimal CalculationValue { get; init; }

        public string? Description { get; init; }

        public IReadOnlyList<int>? UnitIds { get; init; }
    }

    public sealed class ReservationDetailDiscountDto
    {
        public int DiscountId { get; init; }

        /// <summary>UI unit line id (<c>reservation_units.unit_id</c>), when scoped to one unit.</summary>
        public int? UnitId { get; init; }

        public string? UnitLabel { get; init; }

        /// <summary><c>reservation</c> or <c>selectedUnits</c> (derived from <c>apply_on</c> + unit).</summary>
        public string ApplyScope { get; init; } = string.Empty;

        public string ApplyOn { get; init; } = string.Empty;

        public string CalculationMethod { get; init; } = string.Empty;

        public decimal CalculationValue { get; init; }

        public decimal DiscountAmount { get; init; }

        public string? Description { get; init; }

        public DateTime AppliedDate { get; init; }

        public bool IsActive { get; init; }
    }

    public sealed class ReservationDiscountLineDto
    {
        public int DiscountId { get; init; }
        public int? UnitId { get; init; }
        public string ApplyScope { get; init; } = string.Empty;
        public string ApplyOn { get; init; } = string.Empty;
        public string CalculationMethod { get; init; } = string.Empty;
        public decimal CalculationValue { get; init; }
        public decimal DiscountAmount { get; init; }
        public string? Description { get; init; }
        public DateTime AppliedDate { get; init; }
        public bool IsActive { get; init; }
    }

    public sealed class ReservationDiscountApplyResultDto
    {
        public ReservationDiscountLineDto Discount { get; init; } = null!;
        public IReadOnlyList<ReservationDetailDiscountDto> Discounts { get; init; } = Array.Empty<ReservationDetailDiscountDto>();
        public ReservationDetailFinancialDto Financial { get; init; } = null!;
    }
}
