using FinanceLedgerAPI.Models;

namespace zaaerIntegration.Utilities
{
    /// <summary>
    /// Resolves effective daily gross rate from base <c>room_type_rates</c> and optional daily overrides.
    /// </summary>
    public static class RoomTypeRateResolver
    {
        /// <summary>KSA weekend high-rate days: Friday and Saturday.</summary>
        public static bool IsKsaHighWeekday(DateTime date)
        {
            return date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;
        }

        public static decimal ResolveBaseDailyGross(RoomTypeRate? rate, DateTime date)
        {
            if (rate == null)
            {
                return 0m;
            }

            if (IsKsaHighWeekday(date))
            {
                return rate.DailyRateHighWeekdays
                    ?? rate.DailyRateMin
                    ?? rate.DailyRateLowWeekdays
                    ?? 0m;
            }

            return rate.DailyRateLowWeekdays
                ?? rate.DailyRateMin
                ?? rate.DailyRateHighWeekdays
                ?? 0m;
        }

        public static decimal ResolveDailyGross(
            RoomTypeRate? baseRate,
            IReadOnlyDictionary<DateTime, decimal> dailyOverrides,
            DateTime date)
        {
            var day = date.Date;
            if (dailyOverrides.TryGetValue(day, out var overrideRate) && overrideRate > 0m)
            {
                return overrideRate;
            }

            return ResolveBaseDailyGross(baseRate, day);
        }

        public static HashSet<int> BuildRateLookupKeys(RoomType roomType)
        {
            var keys = new HashSet<int> { roomType.RoomTypeId };
            if (roomType.ZaaerId.HasValue && roomType.ZaaerId.Value > 0)
            {
                keys.Add(roomType.ZaaerId.Value);
            }

            var linkId = PropertyEntityLinks.GetRoomTypeLinkId(roomType);
            if (linkId.HasValue)
            {
                keys.Add(linkId.Value);
            }

            return keys;
        }

        public static bool RateMatchesRoomType(RoomTypeRate rate, HashSet<int> rateKeys)
        {
            return rateKeys.Contains(rate.RoomTypeId)
                || (rate.ZaaerId.HasValue && rateKeys.Contains(rate.ZaaerId.Value));
        }
    }
}
