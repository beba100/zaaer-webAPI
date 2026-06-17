using DevExpress.XtraReports.UI;
using zaaerIntegration.Reporting.Abstractions;
using zaaerIntegration.Reporting.Factories.Invoice;
using zaaerIntegration.Reporting.Services.Invoice;

namespace zaaerIntegration.Reporting.Services.Viewer;

public interface IReportDocumentService
{
    Task<XtraReport> CreateBoundReportAsync(ReportContext context, CancellationToken cancellationToken = default);
}

public sealed class ReportDocumentService : IReportDocumentService
{
    private readonly IInvoiceReportDataService _invoiceDataService;
    private readonly IInvoiceReportFactory _invoiceFactory;

    public ReportDocumentService(
        IInvoiceReportDataService invoiceDataService,
        IInvoiceReportFactory invoiceFactory)
    {
        _invoiceDataService = invoiceDataService;
        _invoiceFactory = invoiceFactory;
    }

    public async Task<XtraReport> CreateBoundReportAsync(
        ReportContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(context.ReportKey, ReportKeys.Invoice, StringComparison.OrdinalIgnoreCase))
        {
            var invoiceId = context.GetRequiredInt("invoiceId");
            var dto = await _invoiceDataService.GetAsync(invoiceId, cancellationToken);
            var report = _invoiceFactory.Create(context);
            _invoiceFactory.BindData(report, dto);
            return report;
        }

        throw new NotSupportedException($"Report '{context.ReportKey}' is not supported by the document viewer.");
    }
}
