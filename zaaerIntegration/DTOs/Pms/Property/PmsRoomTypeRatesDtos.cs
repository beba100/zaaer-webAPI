namespace zaaerIntegration.DTOs.Pms.Property
{
    public sealed class PmsRoomTypeRateListItemDto
    {
        public int RateId { get; set; }
        public int RoomTypeId { get; set; }
        public int? RoomTypeZaaerId { get; set; }
        public string RoomTypeName { get; set; } = string.Empty;
        public string? RoomTypeNameEn { get; set; }
        public decimal? DailyRateLowWeekdays { get; set; }
        public decimal? DailyRateHighWeekdays { get; set; }
        public decimal? DailyRateMin { get; set; }
        public decimal? MonthlyRate { get; set; }
        public decimal? MonthlyRateMin { get; set; }
        public decimal? OtaRateLowWeekdays { get; set; }
        public decimal? OtaRateHighWeekdays { get; set; }
    }

    public sealed class PmsUpdateRoomTypeRateDto
    {
        public int RoomTypeId { get; set; }
        public decimal? DailyRateLowWeekdays { get; set; }
        public decimal? DailyRateHighWeekdays { get; set; }
        public decimal? DailyRateMin { get; set; }
        public decimal? MonthlyRate { get; set; }
        public decimal? MonthlyRateMin { get; set; }
        public decimal? OtaRateLowWeekdays { get; set; }
        public decimal? OtaRateHighWeekdays { get; set; }
    }

    public sealed class PmsRatesCalendarDayDto
    {
        public string Date { get; set; } = string.Empty;
        public string DayLabel { get; set; } = string.Empty;
        public bool IsWeekend { get; set; }
    }

    public sealed class PmsRatesCalendarCellDto
    {
        public string Date { get; set; } = string.Empty;
        public int? TotalUnits { get; set; }
        public int? AvailableUnits { get; set; }
        public decimal? Price { get; set; }
        public bool IsOverride { get; set; }
    }

    public sealed class PmsRatesCalendarRowDto
    {
        public int RoomTypeId { get; set; }
        public string RoomTypeName { get; set; } = string.Empty;
        /// <summary>availability | price</summary>
        public string RowKind { get; set; } = string.Empty;
        public string? RowLabel { get; set; }
        public List<PmsRatesCalendarCellDto> Cells { get; set; } = new();
    }

    public sealed class PmsRatesCalendarDto
    {
        public string FromDate { get; set; } = string.Empty;
        public string ToDate { get; set; } = string.Empty;
        public List<PmsRatesCalendarDayDto> Days { get; set; } = new();
        public List<PmsRatesCalendarRowDto> Rows { get; set; } = new();
    }

    public sealed class PmsUpsertDailyRatesDto
    {
        public int RoomTypeId { get; set; }
        public string DateFrom { get; set; } = string.Empty;
        public string DateTo { get; set; } = string.Empty;
        /// <summary>Null removes overrides for the range.</summary>
        public decimal? GrossRate { get; set; }
    }
}
