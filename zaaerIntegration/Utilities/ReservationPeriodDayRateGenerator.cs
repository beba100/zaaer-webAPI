using FinanceLedgerAPI.Enums;
using FinanceLedgerAPI.Models;

namespace zaaerIntegration.Utilities
{
    /// <summary>Date/night helpers and day-rate row planning for <c>reservation_periods</c>.</summary>
    public static class ReservationPeriodDayRateGenerator
    {
        public static int CountHotelNights(DateTime checkIn, DateTime checkOut)
        {
            if (checkOut.Date <= checkIn.Date)
            {
                return 0;
            }

            return Math.Max(1, (int)(checkOut.Date - checkIn.Date).TotalDays);
        }

        public static bool IsMonthlyRental(string? rentalType)
        {
            return NormalizeRentalType(rentalType).Contains("month", StringComparison.Ordinal);
        }

        public static string NormalizeRentalType(string? rentalType)
        {
            if (string.IsNullOrWhiteSpace(rentalType))
            {
                return "daily";
            }

            var v = rentalType.Trim();
            if (RentalTypeHelper.TryParseStorage(rentalType, out var parsed))
            {
                return RentalTypeHelper.ToStorageValue(parsed);
            }

            return v.ToLowerInvariant();
        }

        /// <summary>
        /// Night dates to materialize in <c>reservation_unit_day_rates</c> for a period segment.
        /// <paramref name="toDate"/> is checkout/departure (exclusive for nightly expansion).
        /// </summary>
        public static IReadOnlyList<DateTime> EnumerateNightDates(DateTime fromDate, DateTime toDate, string rentalType)
        {
            fromDate = fromDate.Date;
            toDate = toDate.Date;

            if (IsMonthlyRental(rentalType))
            {
                return new[] { fromDate };
            }

            var nights = CountHotelNights(fromDate, toDate);
            if (nights <= 0)
            {
                return Array.Empty<DateTime>();
            }

            var list = new List<DateTime>(nights);
            for (var i = 0; i < nights; i++)
            {
                list.Add(fromDate.AddDays(i));
            }

            return list;
        }

        /// <summary>
        /// Night dates protected from delete/regen because they belong to closed pricing periods.
        /// </summary>
        public static HashSet<DateTime> CollectProtectedNightDates(IEnumerable<ReservationPeriod> closedPeriods)
        {
            var set = new HashSet<DateTime>();
            foreach (var period in closedPeriods)
            {
                if (!string.Equals(period.Status, ReservationPeriodStatus.Closed, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var night in EnumerateNightDates(period.FromDate, period.ToDate, period.RentalType))
                {
                    set.Add(night.Date);
                }
            }

            return set;
        }

        public static decimal ResolvePerNightGross(decimal periodGrossRate, string rentalType, DateTime fromDate, DateTime toDate)
        {
            if (IsMonthlyRental(rentalType))
            {
                return periodGrossRate;
            }

            var nights = CountHotelNights(fromDate.Date, toDate.Date);
            if (nights <= 0)
            {
                return periodGrossRate;
            }

            return Math.Round(periodGrossRate, 2, MidpointRounding.AwayFromZero);
        }
    }
}
