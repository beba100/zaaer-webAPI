#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.RoomBoard
{
    public sealed class RoomBoardRequestDto
    {
        public DateTime? Date { get; init; }
        public DateTime? FromDate { get; init; }
        public DateTime? ToDate { get; init; }
        public int? HotelId { get; init; }
        public int? BuildingId { get; init; }
        public string? BuildingIds { get; init; }
        public int? FloorId { get; init; }
        public string? FloorIds { get; init; }
        public int? RoomTypeId { get; init; }
        public string? RoomTypeIds { get; init; }
        public string? Status { get; init; }
        public string? Statuses { get; init; }
        public string? Alert { get; init; }
        public string? Search { get; init; }
        public string? ViewMode { get; init; }

        public DateTime EffectiveFromDate => (FromDate ?? Date ?? DateTime.Today).Date;

        public DateTime EffectiveToDate => (ToDate ?? Date ?? DateTime.Today).Date;
    }
}
