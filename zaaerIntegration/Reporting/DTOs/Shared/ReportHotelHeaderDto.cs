namespace zaaerIntegration.Reporting.DTOs.Shared;

public sealed class ReportHotelHeaderDto
{
    public string? HotelCode { get; init; }
    public string? HotelName { get; init; }
    public string? HotelNameEn { get; init; }
    public string? CompanyName { get; init; }
    public byte[]? LogoBytes { get; init; }
    public string? TaxNumber { get; init; }
    public string? CrNumber { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(CompanyName) ? CompanyName!
        : !string.IsNullOrWhiteSpace(HotelName) ? HotelName!
        : HotelNameEn ?? string.Empty;
}
