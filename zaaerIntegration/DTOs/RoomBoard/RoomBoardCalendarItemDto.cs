#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.RoomBoard
{
    public sealed class RoomBoardCalendarItemDto
    {
        public string Id { get; init; } = string.Empty;
        public int ApartmentId { get; init; }
        public string Text { get; init; } = string.Empty;
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public string Type { get; init; } = string.Empty;
        public string StatusCssClass { get; init; } = string.Empty;
        public string ReservationNo { get; init; } = string.Empty;
        public string GuestName { get; init; } = string.Empty;
        public string StatusLabel { get; init; } = string.Empty;
        public string RentalType { get; init; } = string.Empty;
        public int? ReservationId { get; init; }
        public int? UnitId { get; init; }
    }
}
