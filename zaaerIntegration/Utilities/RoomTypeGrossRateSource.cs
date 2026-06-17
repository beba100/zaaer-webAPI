namespace zaaerIntegration.Utilities
{
    /// <summary>Where an effective gross list price was resolved from.</summary>
    public enum RoomTypeGrossRateSource
    {
        None = 0,
        DailyOverride = 1,
        BaseRates = 2,
        RoomTypeFallback = 3,
        ProgrammaticFallback = 4
    }

    public static class RoomTypeGrossRateSourceCodes
    {
        public const string DailyOverride = "daily_override";
        public const string BaseRates = "base_rates";
        public const string RoomTypeFallback = "room_type_fallback";
        public const string ProgrammaticFallback = "programmatic_fallback";
        public const string None = "none";

        public static string ToCode(RoomTypeGrossRateSource source) =>
            source switch
            {
                RoomTypeGrossRateSource.DailyOverride => DailyOverride,
                RoomTypeGrossRateSource.BaseRates => BaseRates,
                RoomTypeGrossRateSource.RoomTypeFallback => RoomTypeFallback,
                RoomTypeGrossRateSource.ProgrammaticFallback => ProgrammaticFallback,
                _ => None
            };
    }
}
