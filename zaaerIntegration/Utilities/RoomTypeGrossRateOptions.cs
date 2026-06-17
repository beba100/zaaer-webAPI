namespace zaaerIntegration.Utilities
{
    public sealed class RoomTypeGrossRateOptions
    {
        /// <summary>standard | programmatic</summary>
        public string RateFallbackMode { get; init; } = "standard";

        public decimal? FallbackMin { get; init; }
        public decimal? FallbackMax { get; init; }

        public static RoomTypeGrossRateOptions Standard { get; } = new();

        public static RoomTypeGrossRateOptions FromBookingSettings(
            string? rateFallbackMode,
            decimal? fallbackMin,
            decimal? fallbackMax)
        {
            var mode = (rateFallbackMode ?? "standard").Trim().ToLowerInvariant();
            if (mode != "programmatic")
            {
                return Standard;
            }

            return new RoomTypeGrossRateOptions
            {
                RateFallbackMode = "programmatic",
                FallbackMin = fallbackMin,
                FallbackMax = fallbackMax
            };
        }

        public bool UseProgrammaticFallback =>
            string.Equals(RateFallbackMode, "programmatic", StringComparison.OrdinalIgnoreCase);
    }
}
