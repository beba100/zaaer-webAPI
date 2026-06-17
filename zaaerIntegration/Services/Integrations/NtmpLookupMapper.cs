using FinanceLedgerAPI.Models;

namespace zaaerIntegration.Services.Integrations
{
    public sealed class NtmpLookupMapper
    {
        public string MapNationalityCode(Nationality? nationality) =>
            string.IsNullOrWhiteSpace(nationality?.NmtpCode) ? "630" : nationality!.NmtpCode!.Trim();

        public string MapGender(string? gender)
        {
            if (string.IsNullOrWhiteSpace(gender))
            {
                return "0";
            }

            var g = gender.Trim().ToLowerInvariant();
            if (g.StartsWith('m') || (g.Contains("male", StringComparison.Ordinal) && !g.Contains("female", StringComparison.Ordinal)))
            {
                return "1";
            }

            if (g.StartsWith('f') || g.Contains("female", StringComparison.Ordinal))
            {
                return "2";
            }

            return "0";
        }

        public string MapCustomerType(GuestType? guestType)
        {
            if (guestType == null)
            {
                return "3";
            }

            var name = (guestType.GtypeName ?? string.Empty).ToLowerInvariant();
            if (name.Contains("citizen") || name.Contains("مواطن"))
            {
                return "1";
            }

            if (name.Contains("gulf") || name.Contains("خليج"))
            {
                return "2";
            }

            if (name.Contains("resident") || name.Contains("مقيم"))
            {
                return "4";
            }

            return "3";
        }

        public string MapPurposeOfVisit(VisitPurpose? purpose) => "7";

        public string MapRoomRentType(string? rentalType)
        {
            var norm = (rentalType ?? "Daily").Trim().ToLowerInvariant();
            return norm.Contains("month") ? "2" : "1";
        }

        public string MapRoomType(RoomType? roomType) => "2";

        public static string FormatYmd(DateTime? date)
        {
            if (!date.HasValue)
            {
                return "0";
            }

            var d = date.Value.Date;
            return $"{d.Year:0000}{d.Month:00}{d.Day:00}";
        }

        public static string FormatHhmmss(DateTime? dateTime)
        {
            if (!dateTime.HasValue)
            {
                return "0";
            }

            var t = dateTime.Value;
            return $"{t.Hour:00}{t.Minute:00}{t.Second:00}";
        }

        public static string FormatAmount(decimal? value) =>
            Math.Round(value ?? 0m, 2, MidpointRounding.AwayFromZero).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }
}
