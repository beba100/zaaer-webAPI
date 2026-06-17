namespace zaaerIntegration.Reporting.DTOs.Invoice;

public sealed class InvoiceReportPaymentSummaryDto
{
    public IReadOnlyList<InvoiceReportPaymentRowDto> Receipts { get; init; } = Array.Empty<InvoiceReportPaymentRowDto>();
    public decimal TotalPaid { get; init; }
}

public sealed class InvoiceReportPaymentRowDto
{
    public string ReceiptNo { get; init; } = string.Empty;
    public DateTime ReceiptDate { get; init; }
    public decimal AmountPaid { get; init; }
    public string? PaymentMethod { get; init; }
}
