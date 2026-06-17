namespace zaaerIntegration.Reporting.Abstractions;

public interface IReportRegistry
{
    IReadOnlyList<(string ReportKey, string ReportVersion)> ListReports();

    IReportProvider ResolveProvider(string reportKey, string reportVersion);

    IReportFactory? ResolveFactory(string reportKey, string reportVersion);
}
