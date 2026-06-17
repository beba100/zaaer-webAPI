namespace zaaerIntegration.Utilities
{
    public static class ResortTicketValidityModes
    {
        public const string BusinessDay = "business_day";
        public const string FromFirstScan = "from_first_scan";

        public static readonly string[] All = { BusinessDay, FromFirstScan };

        public static string Normalize(string? value)
        {
            var normalized = value?.Trim().ToLowerInvariant();
            return All.Contains(normalized) ? normalized! : BusinessDay;
        }

        public static bool IsFromFirstScan(string? mode) =>
            string.Equals(Normalize(mode), FromFirstScan, StringComparison.OrdinalIgnoreCase);
    }
}
