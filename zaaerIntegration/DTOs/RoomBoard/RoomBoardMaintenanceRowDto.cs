#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.RoomBoard
{
    public sealed class RoomBoardMaintenanceRowDto
    {
        public int Id { get; init; }
        public DateTime FromDate { get; init; }
        public DateTime ToDate { get; init; }
        public string Reason { get; init; } = string.Empty;
        public string? Comment { get; init; }
        public IReadOnlyList<string> Categories { get; init; } = Array.Empty<string>();
        public string Status { get; init; } = string.Empty;
    }
}
