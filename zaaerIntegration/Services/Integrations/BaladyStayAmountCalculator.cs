namespace zaaerIntegration.Services.Integrations
{
    /// <summary>
    /// Prorates reservation net stay revenue into a calendar month for Balady disclosure.
    /// Monthly «ThirtyDay» stays use 30-day month blocks for the denominator; «Actual» uses calendar days.
    /// </summary>
    internal static class BaladyStayAmountCalculator
    {
        internal const string MonthlyCalendarThirtyDay = "ThirtyDay";
        internal const string MonthlyCalendarActual = "Actual";

        internal sealed record StayBounds(DateTime CheckInDate, DateTime StayEndExclusive);

        internal static StayBounds ResolveStayBounds(DateTime checkInDate, DateTime? checkOutDate, DateTime? departureDate)
        {
            var start = checkInDate.Date;
            var endExclusive = (departureDate ?? checkOutDate)?.Date ?? start.AddDays(1);
            if (endExclusive <= start)
            {
                endExclusive = start.AddDays(1);
            }

            return new StayBounds(start, endExclusive);
        }

        internal static int CountTotalStayDays(
            StayBounds bounds,
            string? rentalType,
            string? monthlyCalendarMode,
            int? numberOfMonths)
        {
            var calendarDays = Math.Max(1, (bounds.StayEndExclusive - bounds.CheckInDate).Days);

            if (!IsMonthlyRental(rentalType))
            {
                return calendarDays;
            }

            if (IsActualMonthlyCalendar(monthlyCalendarMode))
            {
                return calendarDays;
            }

            if (numberOfMonths is > 0)
            {
                return Math.Max(1, numberOfMonths.Value * 30);
            }

            return Math.Max(30, (int)Math.Round(calendarDays / 30.0, MidpointRounding.AwayFromZero) * 30);
        }

        internal static int CountDaysInMonth(
            StayBounds bounds,
            DateTime monthStart,
            DateTime monthEndExclusive)
        {
            var overlapStart = bounds.CheckInDate > monthStart ? bounds.CheckInDate : monthStart;
            var overlapEndExclusive = bounds.StayEndExclusive < monthEndExclusive
                ? bounds.StayEndExclusive
                : monthEndExclusive;

            if (overlapEndExclusive <= overlapStart)
            {
                return 0;
            }

            return (overlapEndExclusive - overlapStart).Days;
        }

        internal static (DateTime? PeriodFrom, DateTime? PeriodTo) ResolvePeriodInMonth(
            StayBounds bounds,
            DateTime monthStart,
            DateTime monthEndExclusive)
        {
            var overlapStart = bounds.CheckInDate > monthStart ? bounds.CheckInDate : monthStart;
            var overlapEndExclusive = bounds.StayEndExclusive < monthEndExclusive
                ? bounds.StayEndExclusive
                : monthEndExclusive;

            if (overlapEndExclusive <= overlapStart)
            {
                return (null, null);
            }

            return (overlapStart, overlapEndExclusive.AddDays(-1));
        }

        internal static decimal CalculateAmount(decimal netStayBase, int totalStayDays, int daysInMonth)
        {
            if (netStayBase <= 0m || totalStayDays <= 0 || daysInMonth <= 0)
            {
                return 0m;
            }

            return Math.Round(
                netStayBase / totalStayDays * daysInMonth,
                2,
                MidpointRounding.AwayFromZero);
        }

        internal static decimal ResolveNetStayBase(
            decimal? subtotal,
            decimal? totalAmount,
            decimal? totalTaxAmount,
            decimal unitRentSum = 0m)
        {
            if (subtotal is > 0m)
            {
                return subtotal.Value;
            }

            if (unitRentSum > 0m)
            {
                return unitRentSum;
            }

            if (totalAmount is > 0m)
            {
                var tax = totalTaxAmount ?? 0m;
                var net = totalAmount.Value - tax;
                if (net > 0m)
                {
                    return net;
                }
            }

            return 0m;
        }

        private static bool IsMonthlyRental(string? rentalType)
        {
            return string.Equals(rentalType?.Trim(), "monthly", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rentalType?.Trim(), "Monthly", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsActualMonthlyCalendar(string? monthlyCalendarMode)
        {
            return string.Equals(
                monthlyCalendarMode?.Trim(),
                MonthlyCalendarActual,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
