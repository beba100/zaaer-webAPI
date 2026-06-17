namespace zaaerIntegration.Utilities
{
    public static class BookingEngineAvailabilityModes
    {
        public const string Actual = "actual";
        public const string Override = "override";
        public const string MinActualOverride = "min";
    }

    public static class BookingEngineAvailabilityHelper
    {
        public static int ResolveDisplayUnits(int actualUnits, int? overrideUnits, string? mode)
        {
            actualUnits = Math.Max(0, actualUnits);
            var normalized = (mode ?? BookingEngineAvailabilityModes.Actual).Trim().ToLowerInvariant();

            if (!overrideUnits.HasValue)
            {
                return actualUnits;
            }

            var cap = Math.Max(0, overrideUnits.Value);

            return normalized switch
            {
                BookingEngineAvailabilityModes.Override => cap,
                BookingEngineAvailabilityModes.MinActualOverride => Math.Min(actualUnits, cap),
                _ => actualUnits
            };
        }
    }
}
