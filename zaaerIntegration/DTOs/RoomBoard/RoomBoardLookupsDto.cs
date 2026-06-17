#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.RoomBoard
{
    public sealed class RoomBoardLookupsDto
    {
        public IReadOnlyList<RoomBoardLookupDto> Buildings { get; init; } = Array.Empty<RoomBoardLookupDto>();
        public IReadOnlyList<RoomBoardLookupDto> Floors { get; init; } = Array.Empty<RoomBoardLookupDto>();
        public IReadOnlyList<RoomBoardLookupDto> RoomTypes { get; init; } = Array.Empty<RoomBoardLookupDto>();
    }
}
