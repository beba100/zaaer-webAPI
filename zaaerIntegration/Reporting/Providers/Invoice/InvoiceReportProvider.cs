using DevExpress.XtraReports.UI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using zaaerIntegration.Reporting.Abstractions;
using zaaerIntegration.Reporting.Base;
using zaaerIntegration.Reporting.DTOs.Invoice;
using zaaerIntegration.Reporting.Factories.Invoice;
using zaaerIntegration.Reporting.Services.Invoice;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Reporting.Providers.Invoice;

public sealed class InvoiceReportProvider : ReportProviderBase
{
    private static readonly TimeSpan PdfCacheDuration = TimeSpan.FromMinutes(2);

    private readonly IInvoiceReportDataService _dataService;
    private readonly IInvoiceReportFactory _factory;
    private readonly IMemoryCache _cache;
    private readonly ITenantService _tenantService;
    private readonly IHostEnvironment _env;

    public InvoiceReportProvider(
        IReportRenderService renderService,
        IInvoiceReportDataService dataService,
        IInvoiceReportFactory factory,
        IMemoryCache cache,
        ITenantService tenantService,
        IHostEnvironment env)
        : base(renderService)
    {
        _dataService = dataService;
        _factory = factory;
        _cache = cache;
        _tenantService = tenantService;
        _env = env;
    }

    public override string ReportKey => ReportKeys.Invoice;
    public override string ReportVersion => ReportVersions.Invoice_v1;

    public override async Task<ReportRenderResult> RenderAsync(
        ReportContext context,
        ReportExportFormat format,
        CancellationToken cancellationToken = default)
    {
        // DEV BYPASS: Skip PDF cache completely during development so you can test the new enterprise invoice immediately
        if (format == ReportExportFormat.Pdf && !_env.IsDevelopment())
        {
            var cacheKey = BuildPdfCacheKey(context);
            if (_cache.TryGetValue(cacheKey, out ReportRenderResult? cached) && cached is not null)
            {
                return cached;
            }

            var result = await base.RenderAsync(context, format, cancellationToken);
            _cache.Set(cacheKey, result, PdfCacheDuration);
            return result;
        }

        return await base.RenderAsync(context, format, cancellationToken);
    }

    protected override async Task<XtraReport> BuildReportAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        var dto = await LoadReportDtoCachedAsync(context, cancellationToken);
        var report = _factory.Create(context);
        _factory.BindData(report, dto);
        return report;
    }

    private async Task<InvoiceReportDto> LoadReportDtoCachedAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildDtoCacheKey(context);
        if (_cache.TryGetValue(cacheKey, out InvoiceReportDto? cached) && cached is not null)
        {
            return cached;
        }

        var dto = context.Parameters.ContainsKey("invoiceZaaerId")
            ? await _dataService.GetByZaaerIdAsync(context.GetRequiredInt("invoiceZaaerId"), cancellationToken)
            : await _dataService.GetAsync(context.GetRequiredInt("invoiceId"), cancellationToken);

        _cache.Set(cacheKey, dto, PdfCacheDuration);
        return dto;
    }

    private string BuildDtoCacheKey(ReportContext context)
    {
        var hotel = _tenantService.GetTenant()?.Code?.Trim().ToLowerInvariant() ?? "unknown";
        if (context.Parameters.ContainsKey("invoiceZaaerId"))
        {
            return $"report:invoice:dto:{hotel}:z{context.GetRequiredInt("invoiceZaaerId")}";
        }

        return $"report:invoice:dto:{hotel}:i{context.GetRequiredInt("invoiceId")}";
    }

    private string BuildPdfCacheKey(ReportContext context)
    {
        var hotel = _tenantService.GetTenant()?.Code?.Trim().ToLowerInvariant() ?? "unknown";
        if (context.Parameters.ContainsKey("invoiceZaaerId"))
        {
            return $"report:invoice:pdf:{hotel}:z{context.GetRequiredInt("invoiceZaaerId")}";
        }

        return $"report:invoice:pdf:{hotel}:i{context.GetRequiredInt("invoiceId")}";
    }

    protected override Task<string> BuildFileNameAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        if (context.Parameters.ContainsKey("invoiceZaaerId"))
        {
            var zaaerId = context.GetRequiredInt("invoiceZaaerId");
            return Task.FromResult($"invoice-z{zaaerId}");
        }

        var invoiceId = context.GetRequiredInt("invoiceId");
        return Task.FromResult($"invoice-{invoiceId}");
    }
}
