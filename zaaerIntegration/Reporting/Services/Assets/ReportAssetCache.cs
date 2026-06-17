using Microsoft.Extensions.Caching.Memory;
using zaaerIntegration.Reporting.Abstractions;

namespace zaaerIntegration.Reporting.Services.Assets;

public sealed class ReportAssetCache : IReportAssetCache
{
    private static readonly TimeSpan LogoCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan HeaderCacheDuration = TimeSpan.FromHours(1);

    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ReportAssetCache> _logger;

    public ReportAssetCache(
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory,
        ILogger<ReportAssetCache> logger)
    {
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<byte[]?> GetLogoBytesAsync(
        string hotelCode,
        string? logoUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
        {
            return null;
        }

        var cacheKey = $"report:logo:{hotelCode.Trim().ToLowerInvariant()}:{logoUrl.Trim()}";
        if (_cache.TryGetValue(cacheKey, out byte[]? cached))
        {
            return cached;
        }

        var bytes = await LoadLogoBytesAsync(logoUrl, cancellationToken);
        if (bytes is not null)
        {
            _cache.Set(cacheKey, bytes, LogoCacheDuration);
        }

        return bytes;
    }

    public async Task<ReportHotelHeaderCacheEntry?> GetHotelHeaderAsync(
        string hotelCode,
        Func<CancellationToken, Task<ReportHotelHeaderCacheEntry>> factory,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"report:header:{hotelCode.Trim().ToLowerInvariant()}";
        if (_cache.TryGetValue(cacheKey, out ReportHotelHeaderCacheEntry? cached) && cached is not null)
        {
            return cached;
        }

        var entry = await factory(cancellationToken);
        _cache.Set(cacheKey, entry, HeaderCacheDuration);
        return entry;
    }

    public void InvalidateHotel(string hotelCode)
    {
        // Prefix-based invalidation is not supported by IMemoryCache without tracking keys.
        // Callers can rely on TTL; explicit invalidation clears known header key only.
        var cacheKey = $"report:header:{hotelCode.Trim().ToLowerInvariant()}";
        _cache.Remove(cacheKey);
    }

    private async Task<byte[]?> LoadLogoBytesAsync(string logoUrl, CancellationToken cancellationToken)
    {
        try
        {
            if (logoUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var comma = logoUrl.IndexOf(',');
                if (comma < 0)
                {
                    return null;
                }

                return Convert.FromBase64String(logoUrl[(comma + 1)..]);
            }

            if (Uri.TryCreate(logoUrl, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                var client = _httpClientFactory.CreateClient("ReportAssets");
                return await client.GetByteArrayAsync(uri, cancellationToken);
            }

            if (File.Exists(logoUrl))
            {
                return await File.ReadAllBytesAsync(logoUrl, cancellationToken);
            }

            var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", logoUrl.TrimStart('/'));
            if (File.Exists(webRootPath))
            {
                return await File.ReadAllBytesAsync(webRootPath, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load report logo from {LogoUrl}", logoUrl);
        }

        return null;
    }
}
