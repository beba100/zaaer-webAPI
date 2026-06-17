#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.Pms.ReservationDetail
{
    public sealed class ReservationUnitDayRateDto
    {
        public int? RateId { get; init; }
        public int ReservationId { get; init; }
        public int UnitId { get; init; }
        public DateTime NightDate { get; init; }
        public decimal GrossRate { get; init; }
        public decimal? EwaAmount { get; init; }
        public decimal? VatAmount { get; init; }
        public decimal? NetAmount { get; init; }
        public bool IsManual { get; init; }
    }

    public sealed class ReservationUnitDayRateSummaryDto
    {
        public decimal VatRate { get; init; }
        public decimal EwaRate { get; init; }
        public bool TaxIncluded { get; init; } = true;
        public decimal Subtotal { get; init; }
        public decimal EwaAmount { get; init; }
        public decimal VatAmount { get; init; }
        public decimal Total { get; init; }
    }

    public sealed class ReservationUnitDayRatesResponseDto
    {
        public int ReservationId { get; init; }
        public int? UnitId { get; init; }
        public ReservationUnitDayRateSummaryDto Summary { get; init; } = new();
        public IReadOnlyList<ReservationUnitDayRateDto> Items { get; init; } = Array.Empty<ReservationUnitDayRateDto>();
    }

    public sealed class ReservationUnitDayRateSaveDto
    {
        public int? RateId { get; init; }
        public int UnitId { get; init; }
        public DateTime NightDate { get; init; }
        public decimal GrossRate { get; init; }
    }

    public sealed class ReservationUnitDayRatesSaveRequestDto
    {
        public int? UnitId { get; init; }
        public IReadOnlyList<ReservationUnitDayRateSaveDto> Items { get; init; } = Array.Empty<ReservationUnitDayRateSaveDto>();
    }
}
