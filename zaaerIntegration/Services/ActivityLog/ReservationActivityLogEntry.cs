namespace zaaerIntegration.Services.ActivityLog
{
    public sealed class ReservationActivityLogEntry
    {
        public required string EventKey { get; init; }
        public required int HotelId { get; init; }
        public int? ReservationId { get; init; }
        public string? ReservationNo { get; init; }
        public int? UnitId { get; init; }
        public string? RefType { get; init; }
        public int? RefId { get; init; }
        public string? RefNo { get; init; }
        public decimal? AmountFrom { get; init; }
        public decimal? AmountTo { get; init; }
        public string? IconKey { get; init; }
        public object? Payload { get; init; }
        public string? ActorDisplayName { get; init; }
        public string Source { get; init; } = "pms";
        public int? ZaaerId { get; init; }
    }
}
