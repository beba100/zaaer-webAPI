using DevExpress.XtraReports.UI;

namespace zaaerIntegration.Reporting.Abstractions;

public interface IReportFactory
{
    string ReportKey { get; }
    string ReportVersion { get; }

    XtraReport Create(ReportContext context);

    void BindData(XtraReport report, object dto);
}
