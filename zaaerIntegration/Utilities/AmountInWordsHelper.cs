namespace zaaerIntegration.Utilities;

/// <summary>Invoice total in Arabic / English words (SAR).</summary>
public static class AmountInWordsHelper
{
    public static (string Arabic, string English) FormatSar(decimal amount)
    {
        var rounded = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        var riyals = (long)Math.Floor(rounded);
        var halalas = (int)Math.Round((rounded - riyals) * 100m, 0, MidpointRounding.AwayFromZero);
        if (halalas == 100)
        {
            riyals++;
            halalas = 0;
        }

        return (FormatArabic(riyals, halalas), FormatEnglish(riyals, halalas));
    }

    private static string FormatArabic(long riyals, int halalas)
    {
        var riyalWords = riyals == 0 ? "صفر" : NumberToArabicWords(riyals);
        var parts = new List<string> { $"فقط {riyalWords} ريال سعودي" };
        if (halalas > 0)
        {
            parts.Add($"و {NumberToArabicWords(halalas)} هللة");
        }

        parts.Add("لا غير");
        return string.Join(" ", parts);
    }

    private static string FormatEnglish(long riyals, int halalas)
    {
        var parts = new List<string>();
        if (riyals == 0 && halalas == 0)
        {
            return "Zero Saudi Riyals only";
        }

        if (riyals > 0)
        {
            parts.Add($"{NumberToEnglishWords(riyals)} Saudi Riyal{(riyals == 1 ? "" : "s")}");
        }

        if (halalas > 0)
        {
            if (parts.Count > 0)
            {
                parts.Add("and");
            }

            parts.Add($"{NumberToEnglishWords(halalas)} Halala{(halalas == 1 ? "" : "s")}");
        }

        parts.Add("only");
        return string.Join(" ", parts);
    }

    private static string NumberToEnglishWords(long number)
    {
        if (number == 0)
        {
            return "zero";
        }

        if (number < 0)
        {
            return $"minus {NumberToEnglishWords(Math.Abs(number))}";
        }

        var units = new[]
        {
            "", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
            "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"
        };
        var tens = new[] { "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };

        string UnderThousand(int n)
        {
            if (n == 0)
            {
                return string.Empty;
            }

            if (n < 20)
            {
                return units[n];
            }

            if (n < 100)
            {
                return tens[n / 10] + (n % 10 > 0 ? " " + units[n % 10] : string.Empty);
            }

            return units[n / 100] + " hundred" + (n % 100 > 0 ? " " + UnderThousand(n % 100) : string.Empty);
        }

        var scales = new (long Value, string Name)[]
        {
            (1_000_000_000L, "billion"),
            (1_000_000L, "million"),
            (1_000L, "thousand")
        };

        var parts = new List<string>();
        var remaining = number;
        foreach (var (value, name) in scales)
        {
            if (remaining >= value)
            {
                var count = remaining / value;
                remaining %= value;
                parts.Add($"{UnderThousand((int)count)} {name}");
            }
        }

        if (remaining > 0)
        {
            parts.Add(UnderThousand((int)remaining));
        }

        return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static string NumberToArabicWords(long number)
    {
        if (number == 0)
        {
            return "صفر";
        }

        if (number < 0)
        {
            return $"سالب {NumberToArabicWords(Math.Abs(number))}";
        }

        var ones = new[]
        {
            "", "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة", "ستة", "سبعة", "ثمانية", "تسعة", "عشرة",
            "أحد عشر", "اثنا عشر", "ثلاثة عشر", "أربعة عشر", "خمسة عشر", "ستة عشر", "سبعة عشر", "ثمانية عشر", "تسعة عشر"
        };
        var tens = new[] { "", "", "عشرون", "ثلاثون", "أربعون", "خمسون", "ستون", "سبعون", "ثمانون", "تسعون" };
        var hundreds = new[] { "", "مائة", "مائتان", "ثلاثمائة", "أربعمائة", "خمسمائة", "ستمائة", "سبعمائة", "ثمانمائة", "تسعمائة" };

        string UnderThousand(int n)
        {
            if (n == 0)
            {
                return string.Empty;
            }

            if (n < 20)
            {
                return ones[n];
            }

            if (n < 100)
            {
                var unit = n % 10;
                var ten = n / 10;
                if (unit == 0)
                {
                    return tens[ten];
                }

                return $"{ones[unit]} و {tens[ten]}";
            }

            var hundred = hundreds[n / 100];
            var rest = UnderThousand(n % 100);
            return string.IsNullOrEmpty(rest) ? hundred : $"{hundred} و {rest}";
        }

        var scales = new (long Value, string Singular, string Dual, string Plural)[]
        {
            (1_000_000_000L, "مليار", "ملياران", "مليارات"),
            (1_000_000L, "مليون", "مليونان", "ملايين"),
            (1_000L, "ألف", "ألفان", "آلاف")
        };

        var parts = new List<string>();
        var remaining = number;
        foreach (var (value, singular, dual, plural) in scales)
        {
            if (remaining < value)
            {
                continue;
            }

            var count = remaining / value;
            remaining %= value;
            parts.Add(ScaleWord(count, singular, dual, plural));
        }

        if (remaining > 0)
        {
            parts.Add(UnderThousand((int)remaining));
        }

        return string.Join(" و ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static string ScaleWord(long count, string singular, string dual, string plural)
    {
        if (count == 1)
        {
            return singular;
        }

        if (count == 2)
        {
            return dual;
        }

        if (count >= 3 && count <= 10)
        {
            return $"{NumberToArabicWords(count)} {plural}";
        }

        return $"{NumberToArabicWords(count)} {singular}";
    }
}
