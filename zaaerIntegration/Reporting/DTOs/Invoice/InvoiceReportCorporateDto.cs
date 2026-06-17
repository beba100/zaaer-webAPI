namespace zaaerIntegration.Reporting.DTOs.Invoice;

public sealed class InvoiceReportCorporateDto
{
    public int CorporateId { get; init; }
    public string? CompanyName { get; init; }
    public string? TaxNumber { get; init; }
    public string? CrNumber { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
}
