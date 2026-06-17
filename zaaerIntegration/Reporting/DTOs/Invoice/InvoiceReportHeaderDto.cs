namespace zaaerIntegration.Reporting.DTOs.Invoice;

public sealed class InvoiceReportHeaderDto
{
    public int InvoiceId { get; init; }
    public int? ZaaerId { get; init; }
    public string InvoiceNo { get; init; } = string.Empty;
    public DateTime InvoiceDate { get; init; }
    public string? InvoiceDateHijri { get; init; }
    public DateTime? PeriodFrom { get; init; }
    public DateTime? PeriodTo { get; init; }
    public string InvoiceType { get; init; } = "rent";
    public string PaymentStatus { get; init; } = "unpaid";
    public string? Notes { get; init; }
    public string? ReservationNo { get; init; }
}
