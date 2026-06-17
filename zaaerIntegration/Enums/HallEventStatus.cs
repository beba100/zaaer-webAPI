namespace FinanceLedgerAPI.Enums
{
    /// <summary>
    /// Operational lifecycle for hall / venue event bookings (3 states, aligned with reservation check-in/out).
    /// </summary>
    public enum HallEventStatus
    {
        Unconfirmed = 0,
        Confirmed = 1,
        Closed = 2
    }

    public static class HallEventStatusCodes
    {
        public const string Unconfirmed = "unconfirmed";
        public const string Confirmed = "confirmed";
        public const string Closed = "closed";

        public static string ToCode(HallEventStatus status) => status switch
        {
            HallEventStatus.Unconfirmed => Unconfirmed,
            HallEventStatus.Confirmed => Confirmed,
            HallEventStatus.Closed => Closed,
            _ => Unconfirmed
        };

        /// <summary>Maps stored / legacy codes to the 3 operational states.</summary>
        public static string NormalizeCode(string? code)
        {
            var normalized = code?.Trim().ToLowerInvariant();
            return normalized switch
            {
                Confirmed or "event_today" or "event_running" => Confirmed,
                Closed or "completed" or "cancelled" => Closed,
                Unconfirmed or "inquiry" or "quotation_sent" or "tentative" or "deposit_paid" => Unconfirmed,
                _ => Unconfirmed
            };
        }

        public static HallEventStatus FromCode(string? code) =>
            NormalizeCode(code) switch
            {
                Confirmed => HallEventStatus.Confirmed,
                Closed => HallEventStatus.Closed,
                _ => HallEventStatus.Unconfirmed
            };

        public static string GetDisplayNameAr(HallEventStatus status) => status switch
        {
            HallEventStatus.Unconfirmed => "غير مؤكد",
            HallEventStatus.Confirmed => "مؤكد",
            HallEventStatus.Closed => "مغلقة",
            _ => "غير معروف"
        };

        public static string GetDisplayNameEn(HallEventStatus status) => status switch
        {
            HallEventStatus.Unconfirmed => "Unconfirmed",
            HallEventStatus.Confirmed => "Confirmed",
            HallEventStatus.Closed => "Closed",
            _ => "Unknown"
        };

        public static string GetStatusColor(HallEventStatus status) => status switch
        {
            HallEventStatus.Unconfirmed => "#94a3b8",
            HallEventStatus.Confirmed => "#f97316",
            HallEventStatus.Closed => "#16a34a",
            _ => "#94a3b8"
        };

        public static IReadOnlyList<(string Value, string LabelEn, string LabelAr, string Color)> ToLookupList()
        {
            return Enum.GetValues<HallEventStatus>()
                .Select(s => (ToCode(s), GetDisplayNameEn(s), GetDisplayNameAr(s), GetStatusColor(s)))
                .ToList();
        }

        public static IReadOnlyList<string> GetAllowedTransitionCodes(HallEventStatus from)
        {
            if (from == HallEventStatus.Confirmed)
            {
                return new[] { Closed };
            }

            return Array.Empty<string>();
        }

        public static bool CanTransition(HallEventStatus from, HallEventStatus to)
        {
            if (from == to)
            {
                return true;
            }

            return (from, to) switch
            {
                (HallEventStatus.Confirmed, HallEventStatus.Closed) => true,
                _ => false
            };
        }
    }
}
