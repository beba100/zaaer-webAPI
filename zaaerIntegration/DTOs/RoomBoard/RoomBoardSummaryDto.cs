#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.RoomBoard
{
    public sealed class RoomBoardSummaryDto
    {
        public int Total { get; init; }
        public int Available { get; init; }
        public int Occupied { get; init; }
        public int Reserved { get; init; }
        public int Cleaning { get; init; }
        public int Maintenance { get; init; }
        public int DepartureToday { get; init; }
        public int Overstay { get; init; }
        public int UnpaidBalance { get; init; }
        public int OccupiedDirty { get; init; }
    }
}
