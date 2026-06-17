using DevExpress.XtraReports.UI;

namespace zaaerIntegration.Reporting.Abstractions;

public interface IReportRenderService
{
    Task<ReportRenderResult> ExportToPdfAsync(
        XtraReport report,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<ReportRenderResult> ExportToXlsxAsync(
        XtraReport report,
        string fileName,
        CancellationToken cancellationToken = default);
}
