namespace zaaerIntegration.DTOs.Pms
{
    public sealed class PmsHotelMonthlyTargetDto
    {
        public int HotelMonthlyTargetId { get; set; }
        public int HotelZaaerId { get; set; }
        public string? BranchName { get; set; }
        public DateTime MonthYear { get; set; }
        public decimal TargetAmount { get; set; }
        public decimal CommissionBefore85 { get; set; }
        public decimal CommissionAt85 { get; set; }
        public decimal Commission86To100 { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public sealed class UpsertPmsHotelMonthlyTargetDto
    {
        public DateTime MonthYear { get; set; }
        public decimal TargetAmount { get; set; }
        public decimal CommissionBefore85 { get; set; }
        public decimal CommissionAt85 { get; set; }
        public decimal Commission86To100 { get; set; }
        public string? BranchName { get; set; }
    }

    public sealed class PmsHotelTargetTierDto
    {
        public string TierKey { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public decimal CommissionRate { get; set; }
        public decimal ThresholdMinPercent { get; set; }
        public decimal ThresholdMaxPercent { get; set; }
        public bool IsReached { get; set; }
        public bool IsActive { get; set; }
        public decimal TierAchievedAmount { get; set; }
        public decimal CommissionAmount { get; set; }
    }

    public sealed class PmsHotelTargetDailyRowDto
    {
        public DateTime Date { get; set; }
        public decimal GrossNet { get; set; }
        public decimal NetExTax { get; set; }
    }

    public sealed class PmsHotelTargetReportDto
    {
        public PmsHotelMonthlyTargetDto? Target { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public decimal AchievedAmount { get; set; }
        public decimal AchievedGrossNet { get; set; }
        public bool UsesVatOnlyNetExTax { get; set; }
        public decimal AchievementPercent { get; set; }
        public decimal RemainingAmount { get; set; }
        public decimal ActiveCommissionRate { get; set; }
        public decimal EstimatedCommissionAmount { get; set; }
        public IReadOnlyList<PmsHotelTargetTierDto> Tiers { get; set; } = Array.Empty<PmsHotelTargetTierDto>();
        public IReadOnlyList<PmsHotelTargetDailyRowDto> DailyItems { get; set; } = Array.Empty<PmsHotelTargetDailyRowDto>();
        public bool HasTarget { get; set; }
    }
}
