using DevExpress.XtraReports.UI;
using zaaerIntegration.Reporting.Abstractions;
using zaaerIntegration.Reporting.Base;
using zaaerIntegration.Reporting.DTOs.Invoice;
using zaaerIntegration.Reporting.Reports.Invoice;

namespace zaaerIntegration.Reporting.Factories.Invoice;

public interface IInvoiceReportFactory : IReportFactory
{
}

public sealed class InvoiceReportFactory : ReportFactoryBase, IInvoiceReportFactory
{
    public override string ReportKey => ReportKeys.Invoice;
    public override string ReportVersion => ReportVersions.Invoice_v1;

    public override XtraReport Create(ReportContext context) => new InvoiceReport_v1();

    public override void BindData(XtraReport report, object dto)
    {
        var typed = RequireReport<InvoiceReport_v1>(report);
        if (dto is not InvoiceReportDto invoiceDto)
        {
            throw new ArgumentException($"Expected {nameof(InvoiceReportDto)}.", nameof(dto));
        }

        typed.BindData(invoiceDto);
    }
}
