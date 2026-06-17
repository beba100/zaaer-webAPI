using zaaerIntegration.Reporting.Abstractions;
using zaaerIntegration.Reporting.Factories.Invoice;
using zaaerIntegration.Reporting.Providers.Invoice;
using zaaerIntegration.Reporting.Services.Assets;
using zaaerIntegration.Reporting.Services.Invoice;
using zaaerIntegration.Reporting.Services.Registry;
using zaaerIntegration.Reporting.Services.Rendering;
using zaaerIntegration.Reporting.Services.Viewer;

namespace zaaerIntegration.Reporting.Extensions;

public static class ReportingServiceCollectionExtensions
{
    public static IServiceCollection AddPmsReporting(this IServiceCollection services)
    {
        services.AddHttpClient("ReportAssets", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddScoped<IReportAssetCache, ReportAssetCache>();
        services.AddScoped<IReportRenderService, ReportRenderService>();
        services.AddScoped<IReportDocumentService, ReportDocumentService>();
        services.AddScoped<IInvoiceReportDataService, InvoiceReportDataService>();
        services.AddScoped<IInvoiceReportFactory, InvoiceReportFactory>();
        services.AddScoped<IReportFactory, InvoiceReportFactory>();
        services.AddScoped<InvoiceReportProvider>();
        services.AddScoped<IReportProvider, InvoiceReportProvider>();
        services.AddScoped<IReportRegistry, ReportRegistry>();

        return services;
    }
}
