#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.Pms.ReservationDetail
{
    public sealed class ReservationPeriodDto
    {
        public int PeriodId { get; init; }
        public int ReservationId { get; init; }
        public int? UnitId { get; init; }
        public string RentalType { get; init; } = string.Empty;
        public DateTime FromDate { get; init; }
        public DateTime ToDate { get; init; }
        public decimal GrossRate { get; init; }
        public bool TaxIncluded { get; init; }
        public string Status { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }

    public sealed class ReservationPeriodListResponseDto
    {
        public int ReservationId { get; init; }
        public bool HasMixedRentalPeriods { get; init; }
        public string? ActiveRentalType { get; init; }
        public IReadOnlyList<ReservationPeriodDto> Items { get; init; } = Array.Empty<ReservationPeriodDto>();
    }

    public sealed class ReservationPeriodAppendRequestDto
    {
        public string RentalType { get; set; } = string.Empty;

        /// <summary>First night / segment start (date only). Default: day after last closed/active period ends.</summary>
        public DateTime? FromDate { get; set; }

        /// <summary>Checkout / departure date for this segment (date only).</summary>
        public DateTime? ToDate { get; set; }

        /// <summary>Alias for <see cref="ToDate"/> when extending stay.</summary>
        public DateTime? NewCheckOutDate { get; set; }

        public decimal? GrossRate { get; set; }
        public int? UnitId { get; set; }
        public bool ClosePreviousPeriod { get; set; } = true;
    }

    public sealed class ReservationPeriodAppendResultDto
    {
        public ReservationPeriodDto Period { get; init; } = null!;
        public ReservationDetailDto? Reservation { get; init; }
    }

    /// <summary>Update the active pricing period (dates/rate/rental type within the active segment).</summary>
    public sealed class ReservationPeriodUpdateRequestDto
    {
        public string? RentalType { get; set; }

        /// <summary>Checkout / departure date for this segment (date only).</summary>
        public DateTime? ToDate { get; set; }

        public decimal? GrossRate { get; set; }
    }
}
