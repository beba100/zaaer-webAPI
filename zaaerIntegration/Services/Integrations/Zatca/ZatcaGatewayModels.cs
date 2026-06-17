namespace zaaerIntegration.Services.Integrations.Zatca
{
    public sealed class ZatcaSubmissionRequest
    {
        public required string Environment { get; init; }
        public required string Profile { get; init; }
        public required string SubmissionMode { get; init; }
        public required string DocumentKind { get; init; }
        public required string DocumentNo { get; init; }
        public required string ZatcaUuid { get; init; }
        public required string SignedXmlBase64 { get; init; }
        public string? InvoiceHash { get; init; }
        /// <summary>ZATCA Fatoora <c>Accept-Language</c>: <c>EN</c> or <c>AR</c>.</summary>
        public string? AcceptLanguage { get; init; }
    }

    public sealed class ZatcaSubmissionResult
    {
        public bool Success { get; init; }
        public int? HttpStatusCode { get; init; }
        public string? ClearanceStatus { get; init; }
        public string? QrBase64 { get; init; }
        public string? ResponseBody { get; init; }
        public string? ErrorMessage { get; init; }
    }
}
