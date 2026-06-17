using zaaerIntegration.Data;
using zaaerIntegration.Reporting.Abstractions;
using zaaerIntegration.Reporting.DTOs.Shared;
using zaaerIntegration.Services.Integrations;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Reporting.Base;

public abstract class PmsReportDataServiceBase : PmsHotelScopeService
{
    protected readonly IReportAssetCache AssetCache;
    protected readonly ITenantService TenantServiceRef;

    protected PmsReportDataServiceBase(
        ApplicationDbContext context,
        ITenantService tenantService,
        IReportAssetCache assetCache)
        : base(context, tenantService)
    {
        AssetCache = assetCache;
        TenantServiceRef = tenantService;
    }

    protected async Task<ReportHotelHeaderDto> BuildHotelHeaderAsync(CancellationToken cancellationToken)
    {
        var tenant = TenantServiceRef.GetTenant()
            ?? throw new UnauthorizedAccessException("Tenant not resolved. Provide X-Hotel-Code.");

        var hotelCode = tenant.Code.Trim();
        var cached = await AssetCache.GetHotelHeaderAsync(
            hotelCode,
            async ct =>
            {
                var hotel = await GetCurrentHotelSettingsAsync(ct);
                var logoBytes = await AssetCache.GetLogoBytesAsync(hotelCode, hotel.LogoUrl, ct);
                return new ReportHotelHeaderCacheEntry
                {
                    HotelCode = hotelCode,
                    HotelName = hotel.HotelName,
                    HotelNameEn = hotel.HotelNameEn,
                    CompanyName = hotel.CompanyName,
                    TaxNumber = hotel.TaxNumber,
                    CrNumber = hotel.CrNumber,
                    Phone = hotel.Phone,
                    Email = hotel.Email,
                    Address = hotel.Address,
                    City = hotel.City,
                    LogoUrl = hotel.LogoUrl,
                    LogoBytes = logoBytes
                };
            },
            cancellationToken);

        return new ReportHotelHeaderDto
        {
            HotelCode = cached.HotelCode,
            HotelName = cached.HotelName,
            HotelNameEn = cached.HotelNameEn,
            CompanyName = cached.CompanyName,
            TaxNumber = cached.TaxNumber,
            CrNumber = cached.CrNumber,
            Phone = cached.Phone,
            Email = cached.Email,
            Address = cached.Address,
            City = cached.City,
            LogoBytes = cached.LogoBytes
        };
    }

    protected static DateTime ReportGeneratedAt() => KsaTime.Now;
}
