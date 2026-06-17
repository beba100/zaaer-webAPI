namespace zaaerIntegration.Reporting.DTOs.Invoice;

public sealed class InvoiceReportTaxSummaryDto
{
    public decimal Subtotal { get; init; }
    public decimal VatRate { get; init; }
    public decimal VatAmount { get; init; }
    public decimal LodgingTaxRate { get; init; }
    public decimal LodgingTaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal AmountPaid { get; init; }
    public decimal AmountRemaining { get; init; }
}
