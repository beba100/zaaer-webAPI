using zaaerIntegration.Utilities;

namespace zaaerIntegration.Security
{
    /// <summary>
    /// Property-type-aware report permission codes (hotel vs resort vs hall).
    /// Parent codes (e.g. hotel.reports) grant all child report keys via <see cref="PermissionService"/>.
    /// </summary>
    public static class PmsReportPermissions
    {
        public const string HotelAll = "hotel.reports";
        public const string ResortAll = "resort.reports";
        public const string HallAll = "hall.reports";

        public static string[] LodgingCodes(string reportKey, string? propertyType)
        {
            var key = NormalizeKey(reportKey);
            if (PropertyTypes.IsResort(propertyType))
            {
                return new[]
                {
                    ResortCode(key),
                    ResortAll,
                    HotelCode(key),
                    HotelAll
                };
            }

            if (PropertyTypes.IsHotel(propertyType))
            {
                return new[]
                {
                    HotelCode(key),
                    HotelAll
                };
            }

            return Array.Empty<string>();
        }

        public static string[] HallCodes(string reportKey)
        {
            var key = NormalizeKey(reportKey);
            return new[]
            {
                HallCode(key),
                HallAll
            };
        }

        public static string[] LodgingCashLedger(string? propertyType)
        {
            if (PropertyTypes.IsResort(propertyType))
            {
                return new[]
                {
                    "resort.reports.cash_ledger",
                    ResortAll,
                    "hotel.reports.cash_ledger",
                    HotelAll,
                    HallAll
                };
            }

            if (PropertyTypes.IsHotel(propertyType))
            {
                return new[]
                {
                    "hotel.reports.cash_ledger",
                    HotelAll,
                    HallAll
                };
            }

            if (PropertyTypes.IsHall(propertyType))
            {
                return HallCodes("cash_ledger");
            }

            return Array.Empty<string>();
        }

        public static string HotelCode(string reportKey) => $"{HotelAll}.{NormalizeKey(reportKey)}";

        public static string ResortCode(string reportKey) => $"{ResortAll}.{NormalizeKey(reportKey)}";

        public static string HallCode(string reportKey) => $"{HallAll}.{NormalizeKey(reportKey)}";

        public static string[] LodgingTargetManage(string? propertyType)
        {
            if (PropertyTypes.IsResort(propertyType))
            {
                return new[]
                {
                    "resort.targets.manage",
                    "hotel.targets.manage",
                    ResortAll,
                    HotelAll
                };
            }

            if (PropertyTypes.IsHotel(propertyType))
            {
                return new[]
                {
                    "hotel.targets.manage",
                    HotelAll
                };
            }

            return Array.Empty<string>();
        }

        private static string NormalizeKey(string reportKey) =>
            string.IsNullOrWhiteSpace(reportKey)
                ? string.Empty
                : reportKey.Trim().ToLowerInvariant().Replace("-", "_");
    }

}
