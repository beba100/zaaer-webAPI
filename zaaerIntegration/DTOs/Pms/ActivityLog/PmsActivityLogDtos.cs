namespace zaaerIntegration.DTOs.Pms.ActivityLog
{
    public sealed class PmsActivityLogItemDto
    {
        public int LogId { get; init; }
        public int HotelId { get; init; }
        public string EventKey { get; init; } = string.Empty;
        public string? IconKey { get; init; }
        public Dictionary<string, object?> Payload { get; init; } = new();
        public string? ActorDisplayName { get; init; }
        public string? ActorUsername { get; init; }
        public string? ActorFirstName { get; init; }
        public string? ActorLastName { get; init; }
        public int? ActorUserId { get; init; }
        public int? ReservationId { get; init; }
        public string? ReservationNo { get; init; }
        public int? UnitId { get; init; }
        public string? RefType { get; init; }
        public int? RefId { get; init; }
        public string? RefNo { get; init; }
        public decimal? AmountFrom { get; init; }
        public decimal? AmountTo { get; init; }
        public DateTime CreatedAt { get; init; }
        public string Source { get; init; } = "pms";
    }

    public sealed class PmsActivityLogQueryDto
    {
        public int? HotelId { get; init; }
        public DateTime? DateFrom { get; init; }
        public DateTime? DateTo { get; init; }
        public int? ReservationId { get; init; }
        public string? ReservationNo { get; init; }
        public string? EventKey { get; init; }
        public int? ActorUserId { get; init; }
        public int Skip { get; init; }
        public int Take { get; init; } = 50;
    }
}
