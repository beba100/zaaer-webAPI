namespace zaaerIntegration.Reporting.DTOs.Shared;

public sealed class ReportFooterDto
{
    public string? FooterText { get; init; }
    public DateTime GeneratedAt { get; init; }
    public string? GeneratedBy { get; init; }
}
