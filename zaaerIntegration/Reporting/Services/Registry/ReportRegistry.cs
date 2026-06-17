using zaaerIntegration.Reporting.Abstractions;

namespace zaaerIntegration.Reporting.Services.Registry;

public sealed class ReportRegistry : IReportRegistry
{
    private readonly IReadOnlyList<IReportProvider> _providers;
    private readonly IReadOnlyList<IReportFactory> _factories;

    public ReportRegistry(
        IEnumerable<IReportProvider> providers,
        IEnumerable<IReportFactory> factories)
    {
        _providers = providers.ToList();
        _factories = factories.ToList();
    }

    public IReadOnlyList<(string ReportKey, string ReportVersion)> ListReports() =>
        _providers
            .Select(p => (p.ReportKey, p.ReportVersion))
            .Distinct()
            .ToList();

    public IReportProvider ResolveProvider(string reportKey, string reportVersion)
    {
        var provider = _providers.FirstOrDefault(p =>
            string.Equals(p.ReportKey, reportKey, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.ReportVersion, reportVersion, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            throw new KeyNotFoundException($"No report provider registered for '{reportKey}' version '{reportVersion}'.");
        }

        return provider;
    }

    public IReportFactory? ResolveFactory(string reportKey, string reportVersion) =>
        _factories.FirstOrDefault(f =>
            string.Equals(f.ReportKey, reportKey, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(f.ReportVersion, reportVersion, StringComparison.OrdinalIgnoreCase));
}
