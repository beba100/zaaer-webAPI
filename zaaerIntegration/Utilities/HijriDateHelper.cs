using System.Globalization;
using System.Text.RegularExpressions;

namespace zaaerIntegration.Utilities;

/// <summary>Gregorian ↔ Um Al-Qura Hijri (Saudi official calendar).</summary>
public static class HijriDateHelper
{
    private static readonly UmAlQuraCalendar Calendar = new();

    /// <summary>Formats as dd/MM/yyyy (e.g. 01/12/1447).</summary>
    public static string FormatDate(DateTime gregorian)
    {
        var d = gregorian.Date;
        var day = Calendar.GetDayOfMonth(d);
        var month = Calendar.GetMonth(d);
        var year = Calendar.GetYear(d);
        return $"{day:00}/{month:00}/{year:0000}";
    }

    public static string ResolveInvoiceHijri(DateTime invoiceDate, string? storedHijri)
    {
        if (!string.IsNullOrWhiteSpace(storedHijri))
        {
            return storedHijri.Trim();
        }

        return FormatDate(invoiceDate);
    }

    /// <summary>Storage form yyyy-MM-dd (Hijri calendar).</summary>
    public static string FormatStorageDate(DateTime gregorian)
    {
        var d = gregorian.Date;
        var day = Calendar.GetDayOfMonth(d);
        var month = Calendar.GetMonth(d);
        var year = Calendar.GetYear(d);
        return $"{year:0000}-{month:00}-{day:00}";
    }

    public static string ResolveEventHijri(DateTime eventDate, string? storedHijri) =>
        !string.IsNullOrWhiteSpace(storedHijri)
            ? NormalizeStorage(storedHijri)
            : FormatStorageDate(eventDate);

    public static string NormalizeStorage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (TryParseStorage(value, out var y, out var m, out var d))
        {
            return $"{y:0000}-{m:00}-{d:00}";
        }

        if (TryParseDisplay(value, out y, out m, out d))
        {
            return $"{y:0000}-{m:00}-{d:00}";
        }

        return value.Trim();
    }

    public static bool TryParseStorage(string? value, out int year, out int month, out int day)
    {
        year = month = day = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var m = Regex.Match(value.Trim(), @"^(\d{4})-(\d{1,2})-(\d{1,2})$");
        if (!m.Success)
        {
            return false;
        }

        year = int.Parse(m.Groups[1].Value);
        month = int.Parse(m.Groups[2].Value);
        day = int.Parse(m.Groups[3].Value);
        return IsValidHijriParts(year, month, day);
    }

    public static bool TryParseDisplay(string? value, out int year, out int month, out int day)
    {
        year = month = day = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var m = Regex.Match(value.Trim(), @"^(\d{1,2})/(\d{1,2})/(\d{4})$");
        if (!m.Success)
        {
            return false;
        }

        day = int.Parse(m.Groups[1].Value);
        month = int.Parse(m.Groups[2].Value);
        year = int.Parse(m.Groups[3].Value);
        return IsValidHijriParts(year, month, day);
    }

    public static bool TryParseFlexible(string? value, out int year, out int month, out int day)
    {
        if (TryParseStorage(value, out year, out month, out day))
        {
            return true;
        }

        return TryParseDisplay(value, out year, out month, out day);
    }

    public static DateTime? HijriToGregorian(int year, int month, int day)
    {
        if (!IsValidHijriParts(year, month, day))
        {
            return null;
        }

        try
        {
            return Calendar.ToDateTime(year, month, day, 0, 0, 0, 0).Date;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    public static DateTime? HijriStringToGregorian(string? value)
    {
        if (!TryParseFlexible(value, out var y, out var m, out var d))
        {
            return null;
        }

        return HijriToGregorian(y, m, d);
    }

    public static (DateTime From, DateTime To)? GetGregorianRangeForHijriMonth(int hijriYear, int hijriMonth)
    {
        if (hijriYear < 1300 || hijriYear > 1600 || hijriMonth is < 1 or > 12)
        {
            return null;
        }

        try
        {
            var from = Calendar.ToDateTime(hijriYear, hijriMonth, 1, 0, 0, 0, 0).Date;
            var days = Calendar.GetDaysInMonth(hijriYear, hijriMonth);
            var to = Calendar.ToDateTime(hijriYear, hijriMonth, days, 0, 0, 0, 0).Date;
            return (from, to);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    public static (DateTime? From, DateTime? To) ResolveGregorianFilterFromHijriParams(
        string? fromDateHijri,
        string? toDateHijri,
        string? eventDateHijri,
        int? hijriYear,
        int? hijriMonth)
    {
        DateTime? from = null;
        DateTime? to = null;

        if (!string.IsNullOrWhiteSpace(eventDateHijri))
        {
            var exact = HijriStringToGregorian(eventDateHijri);
            if (exact.HasValue)
            {
                from = exact.Value.Date;
                to = exact.Value.Date;
            }
        }

        if (hijriYear.HasValue && hijriMonth.HasValue)
        {
            var monthRange = GetGregorianRangeForHijriMonth(hijriYear.Value, hijriMonth.Value);
            if (monthRange.HasValue)
            {
                from = from.HasValue
                    ? (from.Value > monthRange.Value.From ? from.Value : monthRange.Value.From)
                    : monthRange.Value.From;
                to = to.HasValue
                    ? (to.Value < monthRange.Value.To ? to.Value : monthRange.Value.To)
                    : monthRange.Value.To;
            }
        }

        if (!string.IsNullOrWhiteSpace(fromDateHijri))
        {
            var g = HijriStringToGregorian(fromDateHijri);
            if (g.HasValue)
            {
                from = from.HasValue ? (from.Value > g.Value ? from.Value : g.Value) : g.Value;
            }
        }

        if (!string.IsNullOrWhiteSpace(toDateHijri))
        {
            var g = HijriStringToGregorian(toDateHijri);
            if (g.HasValue)
            {
                to = to.HasValue ? (to.Value < g.Value ? to.Value : g.Value) : g.Value;
            }
        }

        return (from, to);
    }

    private static bool IsValidHijriParts(int year, int month, int day)
    {
        if (year < 1300 || year > 1600 || month is < 1 or > 12 || day < 1)
        {
            return false;
        }

        try
        {
            var days = Calendar.GetDaysInMonth(year, month);
            return day <= days;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
