namespace zaaerIntegration.Reporting.Abstractions;

public interface IReportProvider
{
    string ReportKey { get; }
    string ReportVersion { get; }

    Task<ReportRenderResult> RenderAsync(
        ReportContext context,
        ReportExportFormat format,
        CancellationToken cancellationToken = default);
}
