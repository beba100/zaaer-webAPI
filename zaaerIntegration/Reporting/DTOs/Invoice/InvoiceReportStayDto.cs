namespace zaaerIntegration.Reporting.DTOs.Invoice;

public sealed class InvoiceReportStayDto
{
    public string? ReservationNo { get; init; }
    public DateTime? CheckInDate { get; init; }
    public DateTime? CheckOutDate { get; init; }
    public int? Nights { get; init; }
    public string? UnitsText { get; init; }
    public string? PeriodText { get; init; }
}
