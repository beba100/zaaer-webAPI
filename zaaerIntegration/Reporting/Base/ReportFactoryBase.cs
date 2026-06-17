using DevExpress.XtraReports.UI;
using zaaerIntegration.Reporting.Abstractions;

namespace zaaerIntegration.Reporting.Base;

public abstract class ReportFactoryBase : IReportFactory
{
    public abstract string ReportKey { get; }
    public abstract string ReportVersion { get; }

    public abstract XtraReport Create(ReportContext context);

    public abstract void BindData(XtraReport report, object dto);

    protected static TReport RequireReport<TReport>(XtraReport report)
        where TReport : XtraReport
    {
        if (report is not TReport typed)
        {
            throw new InvalidOperationException($"Expected report type {typeof(TReport).Name}.");
        }

        return typed;
    }
}
