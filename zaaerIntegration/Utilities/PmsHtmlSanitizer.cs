using System.Text.RegularExpressions;

namespace zaaerIntegration.Utilities
{
    /// <summary>
    /// Strips dangerous markup from admin-authored booking engine HTML (XSS defense).
    /// </summary>
    public static partial class PmsHtmlSanitizer
    {
        public static string? SanitizeRichText(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return html;
            }

            var value = html;

            value = DangerousTagRegex().Replace(value, string.Empty);
            value = OnEventAttributeRegex().Replace(value, string.Empty);
            value = JavascriptUrlRegex().Replace(value, string.Empty);
            value = DataUrlScriptRegex().Replace(value, string.Empty);

            return value.Trim();
        }

        [GeneratedRegex(@"<\s*/?\s*(script|iframe|object|embed|link|meta|base|form)\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex DangerousTagRegex();

        [GeneratedRegex(@"\s+on[a-z]+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex OnEventAttributeRegex();

        [GeneratedRegex(@"(href|src|xlink:href)\s*=\s*(""javascript:[^""]*""|'javascript:[^']*')", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex JavascriptUrlRegex();

        [GeneratedRegex(@"src\s*=\s*(""data:text/html[^""]*""|'data:text/html[^']*')", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex DataUrlScriptRegex();
    }
}
