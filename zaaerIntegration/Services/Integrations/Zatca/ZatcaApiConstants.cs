namespace zaaerIntegration.Services.Integrations.Zatca
{
    /// <summary>
    /// ZATCA Fatoora gateway hosts — verify against the official developer portal when upgrading.
    /// </summary>
    public static class ZatcaApiConstants
    {
        public const string ServiceName = "ZATCA";

        public const string ProfileSimplified = "simplified";
        public const string ProfileStandard = "standard";

        public const string ModeReporting = "reporting";
        public const string ModeClearance = "clearance";

        /// <summary>Fatoora API requires <c>Accept-Language: EN</c> or <c>AR</c>.</summary>
        public const string AcceptLanguageArabic = "AR";
        public const string AcceptLanguageEnglish = "EN";

        /// <summary>Fatoora reporting/clearance requires <c>Accept-Version: V2</c>.</summary>
        public const string AcceptVersionV2 = "V2";

        public static string NormalizeAcceptLanguage(string? value)
        {
            var v = (value ?? "").Trim();
            if (v.Length == 0)
            {
                return AcceptLanguageArabic;
            }

            if (v.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            {
                return AcceptLanguageEnglish;
            }

            return AcceptLanguageArabic;
        }

        public const string StatusPending = "pending";
        public const string StatusReported = "reported";
        public const string StatusCleared = "cleared";
        public const string StatusFailed = "failed";
        public const string StatusSkipped = "skipped";

        public const string DocumentKindInvoice = "invoice";
        public const string DocumentKindCreditNote = "credit_note";
        public const string DocumentKindDebitNote = "debit_note";

        /// <summary>Values stored in <c>integration_responses.event_type</c> for ZATCA submissions.</summary>
        public static string SubmitIntegrationEventType(string documentKind) =>
            documentKind switch
            {
                DocumentKindCreditNote => "Submit_credit_note",
                DocumentKindDebitNote => "Submit_debit_note",
                _ => "Submit_invoice"
            };

        public static string NormalizeEnvironment(string? environment) =>
            (environment ?? "sandbox").Trim().ToLowerInvariant() switch
            {
                "simulation" or "sim" => "simulation",
                "production" or "prod" or "core" => "production",
                _ => "sandbox"
            };

        /// <summary>Base URL for ZATCA e-invoicing APIs (no trailing slash).</summary>
        public static string ResolveBaseUrl(string? environment) =>
            NormalizeEnvironment(environment) switch
            {
                "simulation" => "https://gw-fatoora.zatca.gov.sa/e-invoicing/simulation",
                "production" => "https://gw-fatoora.zatca.gov.sa/e-invoicing/core",
                _ => "https://gw-fatoora.zatca.gov.sa/e-invoicing/developer-portal"
            };

        public static string ComplianceCsidUrl(string environment) =>
            $"{ResolveBaseUrl(environment)}/compliance";

        public static string ProductionCsidUrl(string environment) =>
            $"{ResolveBaseUrl(environment)}/production/csids";

        public static string ComplianceInvoicesUrl(string environment) =>
            $"{ResolveBaseUrl(environment)}/compliance/invoices";

        public static string ReportingSingleUrl(string environment) =>
            $"{ResolveBaseUrl(environment)}/invoices/reporting/single";

        public static string ClearanceSingleUrl(string environment) =>
            $"{ResolveBaseUrl(environment)}/invoices/clearance/single";

        public static string SubmissionModeForProfile(string profile) =>
            string.Equals(profile, ProfileStandard, StringComparison.OrdinalIgnoreCase)
                ? ModeClearance
                : ModeReporting;
    }
}
