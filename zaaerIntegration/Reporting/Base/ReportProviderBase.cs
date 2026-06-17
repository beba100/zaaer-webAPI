using zaaerIntegration.Reporting.Abstractions;

namespace zaaerIntegration.Reporting.Base;

public abstract class ReportProviderBase : IReportProvider
{
    private readonly IReportRenderService _renderService;

    protected ReportProviderBase(IReportRenderService renderService)
    {
        _renderService = renderService;
    }

    public abstract string ReportKey { get; }
    public abstract string ReportVersion { get; }

    public virtual async Task<ReportRenderResult> RenderAsync(
        ReportContext context,
        ReportExportFormat format,
        CancellationToken cancellationToken = default)
    {
        ValidateContext(context);
        var fileName = await BuildFileNameAsync(context, cancellationToken);
        var report = await BuildReportAsync(context, cancellationToken);

        try
        {
            return format switch
            {
                ReportExportFormat.Pdf => await _renderService.ExportToPdfAsync(report, fileName, cancellationToken),
                ReportExportFormat.Xlsx => await _renderService.ExportToXlsxAsync(report, fileName, cancellationToken),
                _ => throw new NotSupportedException($"Export format '{format}' is not supported.")
            };
        }
        finally
        {
            report.Dispose();
        }
    }

    protected virtual void ValidateContext(ReportContext context)
    {
        if (!string.Equals(context.ReportKey, ReportKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Invalid report key. Expected '{ReportKey}'.", nameof(context));
        }

        if (!string.Equals(context.ReportVersion, ReportVersion, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Invalid report version. Expected '{ReportVersion}'.", nameof(context));
        }
    }

    protected abstract Task<DevExpress.XtraReports.UI.XtraReport> BuildReportAsync(
        ReportContext context,
        CancellationToken cancellationToken);

    protected abstract Task<string> BuildFileNameAsync(
        ReportContext context,
        CancellationToken cancellationToken);
}
