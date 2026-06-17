#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.RoomBoard
{
    public sealed class RoomBoardResponseDto
    {
        public RoomBoardSummaryDto Summary { get; init; } = new();
        public RoomBoardLookupsDto Lookups { get; init; } = new();
        public IReadOnlyList<RoomBoardItemDto> Rooms { get; init; } = Array.Empty<RoomBoardItemDto>();
        public IReadOnlyList<RoomBoardCalendarItemDto> CalendarItems { get; init; } = Array.Empty<RoomBoardCalendarItemDto>();
    }
}
