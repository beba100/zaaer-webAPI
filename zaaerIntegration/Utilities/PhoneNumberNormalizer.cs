using System.Text.RegularExpressions;

namespace zaaerIntegration.Utilities
{
    /// <summary>
    /// Normalizes guest/customer mobile numbers to international digits (e.g. 9665xxxxxxxx).
    /// Matches PMS guest form convention (dial + local → stored without '+').
    /// </summary>
    public static class PhoneNumberNormalizer
    {
        public static string? NormalizeMobileForStorage(string? phone, string? defaultCountryCode = "+966")
        {
            var digits = Regex.Replace(phone ?? string.Empty, @"\D", "");
            if (string.IsNullOrWhiteSpace(digits))
            {
                return null;
            }

            if (digits.StartsWith("966", StringComparison.Ordinal))
            {
                return "966" + digits[3..].TrimStart('0');
            }

            if (digits.StartsWith("05", StringComparison.Ordinal) && digits.Length >= 10)
            {
                return "966" + digits[1..];
            }

            if (digits.StartsWith("5", StringComparison.Ordinal) && digits.Length >= 9)
            {
                return "966" + digits;
            }

            var ccDigits = Regex.Replace(defaultCountryCode ?? "+966", @"\D", "");
            if (string.IsNullOrWhiteSpace(ccDigits))
            {
                ccDigits = "966";
            }

            if (digits.StartsWith("0", StringComparison.Ordinal))
            {
                digits = digits[1..];
            }

            return ccDigits + digits;
        }
    }
}
