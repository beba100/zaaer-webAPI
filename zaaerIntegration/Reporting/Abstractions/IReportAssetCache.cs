namespace zaaerIntegration.Reporting.Abstractions;

public interface IReportAssetCache
{
    Task<byte[]?> GetLogoBytesAsync(string hotelCode, string? logoUrl, CancellationToken cancellationToken = default);

    Task<ReportHotelHeaderCacheEntry?> GetHotelHeaderAsync(
        string hotelCode,
        Func<CancellationToken, Task<ReportHotelHeaderCacheEntry>> factory,
        CancellationToken cancellationToken = default);

    void InvalidateHotel(string hotelCode);
}

public sealed class ReportHotelHeaderCacheEntry
{
    public required string HotelCode { get; init; }
    public string? HotelName { get; init; }
    public string? HotelNameEn { get; init; }
    public string? CompanyName { get; init; }
    public string? TaxNumber { get; init; }
    public string? CrNumber { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? LogoUrl { get; init; }
    public byte[]? LogoBytes { get; init; }
}
