namespace zaaerIntegration.Utilities
{
    /// <summary>
    /// Resort ticket business day windows (KSA local times). Example: issue from 16:00, close at 04:00 next day.
    /// </summary>
    public static class ResortTicketBusinessHours
    {
        public static DateTime? ResolveCurrentBusinessServiceDate(
            DateTime nowKsa,
            TimeSpan issueStartTime,
            TimeSpan dailyCloseTime)
        {
            if (issueStartTime == TimeSpan.Zero && dailyCloseTime == TimeSpan.Zero)
            {
                return nowKsa.Date;
            }

            if (nowKsa.TimeOfDay >= issueStartTime)
            {
                return nowKsa.Date;
            }

            if (nowKsa.TimeOfDay < dailyCloseTime)
            {
                return nowKsa.Date.AddDays(-1);
            }

            return null;
        }

        public static bool IsWithinIssueWindow(
            DateTime nowKsa,
            TimeSpan issueStartTime,
            TimeSpan dailyCloseTime)
        {
            return ResolveCurrentBusinessServiceDate(nowKsa, issueStartTime, dailyCloseTime).HasValue;
        }

        public static DateTime GetBusinessDayStart(DateTime serviceDate, TimeSpan issueStartTime) =>
            serviceDate.Date.Add(issueStartTime);

        public static DateTime GetBusinessDayEnd(DateTime serviceDate, TimeSpan endTime, TimeSpan issueStartTime)
        {
            var start = GetBusinessDayStart(serviceDate, issueStartTime);
            var end = serviceDate.Date.Add(endTime);
            if (end <= start)
            {
                end = end.AddDays(1);
            }

            return end;
        }

        public static TimeSpan ResolveCategoryEndTime(
            string ticketCategory,
            TimeSpan ticketValidityEndTime,
            TimeSpan? gamesValidityEndTime) =>
            string.Equals(ticketCategory, "games", StringComparison.OrdinalIgnoreCase)
                && gamesValidityEndTime.HasValue
                ? gamesValidityEndTime.Value
                : ticketValidityEndTime;

        public static DateTime ComputeValidFrom(DateTime serviceDate, TimeSpan issueStartTime, DateTime issuedAtKsa)
        {
            var dayStart = GetBusinessDayStart(serviceDate, issueStartTime);
            return issuedAtKsa > dayStart ? issuedAtKsa : dayStart;
        }

        /// <summary>Business-day cap for tickets issued before first scan (from_first_scan mode).</summary>
        public static DateTime ComputePreActivationValidTo(
            DateTime serviceDate,
            string ticketCategory,
            TimeSpan issueStartTime,
            TimeSpan ticketValidityEndTime,
            TimeSpan? gamesValidityEndTime)
        {
            var endTime = ResolveCategoryEndTime(ticketCategory, ticketValidityEndTime, gamesValidityEndTime);
            return GetBusinessDayEnd(serviceDate, endTime, issueStartTime);
        }

        public static DateTime ComputeValidTo(
            DateTime serviceDate,
            string ticketCategory,
            TimeSpan issueStartTime,
            TimeSpan ticketValidityEndTime,
            TimeSpan? gamesValidityEndTime,
            int validForMinutes,
            DateTime validFrom)
        {
            var endTime = ResolveCategoryEndTime(ticketCategory, ticketValidityEndTime, gamesValidityEndTime);
            var businessEnd = GetBusinessDayEnd(serviceDate, endTime, issueStartTime);
            if (validForMinutes <= 0)
            {
                return businessEnd;
            }

            var durationEnd = validFrom.AddMinutes(validForMinutes);
            return durationEnd < businessEnd ? durationEnd : businessEnd;
        }

        /// <summary>After first scan: session end = min(now + minutes, business day end).</summary>
        public static DateTime ComputeSessionValidTo(
            DateTime serviceDate,
            string ticketCategory,
            TimeSpan issueStartTime,
            TimeSpan ticketValidityEndTime,
            TimeSpan? gamesValidityEndTime,
            int validForMinutes,
            DateTime sessionStartKsa)
        {
            var businessEnd = ComputePreActivationValidTo(
                serviceDate,
                ticketCategory,
                issueStartTime,
                ticketValidityEndTime,
                gamesValidityEndTime);
            if (validForMinutes <= 0)
            {
                return businessEnd;
            }

            var durationEnd = sessionStartKsa.AddMinutes(validForMinutes);
            return durationEnd < businessEnd ? durationEnd : businessEnd;
        }
    }
}
