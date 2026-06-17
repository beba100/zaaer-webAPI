namespace zaaerIntegration.Reporting.Abstractions;

public sealed class ReportRenderResult
{
    public required byte[] Content { get; init; }
    public required string MimeType { get; init; }
    public required string FileName { get; init; }
}
