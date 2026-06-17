using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using zaaerIntegration.Reporting.Abstractions;

namespace zaaerIntegration.Reporting.Services.Rendering;

public sealed class ReportRenderService : IReportRenderService
{
    public Task<ReportRenderResult> ExportToPdfAsync(
        XtraReport report,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = new MemoryStream();
        report.ExportToPdf(stream);
        var result = new ReportRenderResult
        {
            Content = stream.ToArray(),
            MimeType = "application/pdf",
            FileName = EnsureExtension(fileName, ".pdf")
        };
        return Task.FromResult(result);
    }

    public Task<ReportRenderResult> ExportToXlsxAsync(
        XtraReport report,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = new MemoryStream();
        var options = new XlsxExportOptions();
        report.ExportToXlsx(stream, options);
        var result = new ReportRenderResult
        {
            Content = stream.ToArray(),
            MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName = EnsureExtension(fileName, ".xlsx")
        };
        return Task.FromResult(result);
    }

    private static string EnsureExtension(string fileName, string extension)
    {
        if (fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            return fileName;
        }

        return fileName + extension;
    }
}
