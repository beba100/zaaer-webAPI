#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.RoomBoard
{
    public sealed class CurrentStayDto
    {
        public int ReservationId { get; init; }
        public int UnitId { get; init; }
        public string ReservationNo { get; init; } = string.Empty;
        public int? CustomerId { get; init; }
        public string? CustomerName { get; init; }
        public DateTime CheckInDate { get; init; }
        public DateTime CheckOutDate { get; init; }
        public DateTime? DepartureDate { get; init; }
        public string CheckInDateShort { get; init; } = string.Empty;
        public string CheckOutDateShort { get; init; } = string.Empty;
        public decimal BalanceAmount { get; init; }
        public string RentalType { get; init; } = string.Empty;
        public bool HasMixedRentalPeriods { get; init; }
        public bool IsDepartureToday { get; init; }
        public bool IsOverstay { get; init; }
        public int OverstayDays { get; init; }
        public bool HasUnpaidBalance { get; init; }
        public bool StatusType { get; init; }
        public string Status { get; init; } = string.Empty;
    }
}
