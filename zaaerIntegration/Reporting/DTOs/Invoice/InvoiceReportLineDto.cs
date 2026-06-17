namespace zaaerIntegration.Reporting.DTOs.Invoice;

public sealed class InvoiceReportLineDto
{
    public int LineNumber { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Quantity { get; init; } = 1m;
    public decimal UnitPrice { get; init; }
    public decimal LineTotal { get; init; }
    public decimal VatRate { get; init; }
    public decimal VatAmount { get; init; }
    public decimal LodgingTaxRate { get; init; }
    public decimal LodgingTaxAmount { get; init; }
    public decimal TotalWithVat { get; init; }

    /// <summary>Unit price after line-level discount (for "السعر بعد الخصم" column).</summary>
    public decimal PriceAfterDiscount { get; init; }
}
