#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.RoomBoard
{
    public sealed class NextStayDto
    {
        public int ReservationId { get; init; }
        public int UnitId { get; init; }
        public string ReservationNo { get; init; } = string.Empty;
        public string? CustomerName { get; init; }
        public DateTime CheckInDate { get; init; }
        public DateTime CheckOutDate { get; init; }
        public string Status { get; init; } = string.Empty;
    }
}
