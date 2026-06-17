using zaaerIntegration.Reporting.Abstractions;
using zaaerIntegration.Reporting.DTOs.Shared;

namespace zaaerIntegration.Reporting.DTOs.Invoice;

public sealed class InvoiceReportDto
{
    public string ReportVersion { get; init; } = ReportVersions.Invoice_v1;
    public ReportHotelHeaderDto Header { get; init; } = new();
    public InvoiceReportHeaderDto Invoice { get; init; } = new();
    public InvoiceReportCustomerDto? Customer { get; init; }
    public InvoiceReportCorporateDto? Corporate { get; init; }
    public IReadOnlyList<InvoiceReportLineDto> Lines { get; init; } = Array.Empty<InvoiceReportLineDto>();
    public InvoiceReportTaxSummaryDto Tax { get; init; } = new();
    public InvoiceReportPaymentSummaryDto Payments { get; init; } = new();
    public byte[]? QrImageBytes { get; init; }

    /// <summary>ZATCA TLV bytes (base64-decoded from invoices.zatca_qr) for QR barcode rendering.</summary>
    public byte[]? ZatcaQrTlvBytes { get; init; }

    public string? ZatcaStatus { get; init; }
    public bool ShowZatcaQr { get; init; }
    public ReportFooterDto Footer { get; init; } = new();
    public DateTime GeneratedAt { get; init; }

    /// <summary>invoices.zatca_profile — simplified | standard</summary>
    public string ZatcaProfile { get; init; } = "simplified";

    /// <summary>True when zatca_profile = standard (B2B tax invoice).</summary>
    public bool IsStandardInvoice { get; init; }

    public InvoiceReportStayDto? Stay { get; init; }

    public string InvoiceTitleAr { get; init; } = "فاتورة ضريبية مبسطة";

    public string InvoiceTitleEn { get; init; } = "SIMPLIFIED TAX INVOICE";

    public string? TotalAmountWordsAr { get; init; }

    public string? TotalAmountWordsEn { get; init; }

    public string? OperatorDisplayName { get; init; }

    public byte[]? OperatorSignatureBytes { get; init; }
}
