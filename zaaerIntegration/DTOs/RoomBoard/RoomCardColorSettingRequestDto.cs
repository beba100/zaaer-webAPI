#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.RoomBoard
{
    public sealed class RoomCardColorSettingRequestDto
    {
        public string? OccupiedCardBackColor { get; init; }
        public string? OccupiedHeaderBackColor { get; init; }
        public string? OccupiedGuestBackColor { get; init; }
        public string? OccupiedDatesBackColor { get; init; }
        public string? OccupiedTextColor { get; init; }
    }
}
