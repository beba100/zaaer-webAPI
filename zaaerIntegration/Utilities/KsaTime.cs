using System;

namespace zaaerIntegration.Utilities
{
    /// <summary>
    /// Provides current date/time values aligned with the Saudi Arabia timezone.
    /// Always use this class to ensure timestamps are saved in Saudi Arabia timezone.
    /// </summary>
    public static class KsaTime
    {
        private static readonly TimeZoneInfo SaudiTimeZone = ResolveSaudiTimeZone();

        /// <summary>
        /// Current date/time in Saudi Arabia (Arab Standard Time / Asia/Riyadh).
        /// Use this for CreatedAt, UpdatedAt, and any new timestamps.
        /// </summary>
        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SaudiTimeZone);

        /// <summary>
        /// Exposes UTC now in case it is still needed by the application.
        /// </summary>
        public static DateTime UtcNow => DateTime.UtcNow;

        /// <summary>
        /// Converts an arbitrary UTC timestamp into the Saudi timezone.
        /// </summary>
        public static DateTime ConvertFromUtc(DateTime utcDateTime) => TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, SaudiTimeZone);

        /// <summary>
        /// Converts any DateTime to Saudi Arabia timezone.
        /// Handles UTC, Local, and Unspecified DateTime kinds automatically.
        /// Use this when receiving timestamps from external systems (like Zaaer).
        /// </summary>
        /// <param name="dateTime">The DateTime to convert (can be UTC, Local, or Unspecified)</param>
        /// <returns>DateTime in Saudi Arabia timezone</returns>
        public static DateTime ToSaudiTime(DateTime dateTime)
        {
            if (dateTime.Kind == DateTimeKind.Utc)
            {
                // If UTC, convert directly
                return TimeZoneInfo.ConvertTimeFromUtc(dateTime, SaudiTimeZone);
            }
            else if (dateTime.Kind == DateTimeKind.Local)
            {
                // If Local, convert to UTC first, then to Saudi time
                var utcTime = TimeZoneInfo.ConvertTimeToUtc(dateTime);
                return TimeZoneInfo.ConvertTimeFromUtc(utcTime, SaudiTimeZone);
            }
            else
            {
                // If Unspecified, assume it's UTC (common for external systems)
                var asUtc = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
                return TimeZoneInfo.ConvertTimeFromUtc(asUtc, SaudiTimeZone);
            }
        }

        /// <summary>
        /// Converts a nullable DateTime to Saudi Arabia timezone.
        /// Returns null if the input is null.
        /// </summary>
        /// <param name="dateTime">The nullable DateTime to convert</param>
        /// <returns>DateTime in Saudi Arabia timezone, or null if input was null</returns>
        public static DateTime? ToSaudiTime(DateTime? dateTime)
        {
            if (!dateTime.HasValue)
                return null;

            return ToSaudiTime(dateTime.Value);
        }

        /// <summary>Calendar date with today's KSA time-of-day (for booking-engine check-in).</summary>
        public static DateTime CombineDateWithCurrentTime(DateTime dateOnly)
        {
            var d = dateOnly.Date;
            var now = Now;
            return new DateTime(d.Year, d.Month, d.Day, now.Hour, now.Minute, now.Second, DateTimeKind.Unspecified);
        }

        /// <summary>Calendar date at a fixed KSA time (e.g. departure 18:00).</summary>
        public static DateTime CombineDateAtTime(DateTime dateOnly, int hour, int minute, int second = 0)
        {
            var d = dateOnly.Date;
            return new DateTime(d.Year, d.Month, d.Day, hour, minute, second, DateTimeKind.Unspecified);
        }

        /// <summary>Standard hotel departure time: 6:00 PM KSA on the given calendar day.</summary>
        public static DateTime DefaultDepartureAtSixPm(DateTime dateOnly) => CombineDateAtTime(dateOnly, 18, 0);

        /// <summary>
        /// Normalizes a Gregorian birth date to a KSA calendar day (avoids UTC JSON shifting the stored day).
        /// </summary>
        public static DateTime? ToGregorianBirthDateOnly(DateTime? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return ToSaudiTime(value.Value).Date;
        }

        private static TimeZoneInfo ResolveSaudiTimeZone()
        {
            var candidateIds = new[] { "Arab Standard Time", "Asia/Riyadh" };

            foreach (var id in candidateIds)
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(id);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }

            return TimeZoneInfo.Utc;
        }
    }
}
