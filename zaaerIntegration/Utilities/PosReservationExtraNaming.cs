using System.Text.RegularExpressions;

namespace zaaerIntegration.Utilities
{
    /// <summary>
    /// Naming convention for POS orders linked to <c>reservation_extras</c> (e.g. <c>POS-ORD0045</c>).
    /// </summary>
    public static partial class PosReservationExtraNaming
    {
        public static string BuildItemName(string orderNo) =>
            $"POS-{orderNo.Trim()}";

        public static bool TryParseOrderNo(string? itemName, out string orderNo)
        {
            orderNo = string.Empty;
            if (string.IsNullOrWhiteSpace(itemName))
            {
                return false;
            }

            var match = PosOrderNoRegex().Match(itemName.Trim());
            if (!match.Success)
            {
                return false;
            }

            orderNo = match.Groups["no"].Value;
            return orderNo.Length > 0;
        }

        [GeneratedRegex(@"POS[\s\-_]*(?<no>ORD\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex PosOrderNoRegex();
    }
}
