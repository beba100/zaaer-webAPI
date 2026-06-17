namespace zaaerIntegration.Reporting.DTOs.Invoice;

public sealed class InvoiceReportCustomerDto
{
    public int CustomerId { get; init; }
    public int? ZaaerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string? MobileNo { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? CustomerNo { get; init; }
}
