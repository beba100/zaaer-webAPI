#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.Pms
{
    public sealed class PmsHallBookingsReportDto
    {
        public IReadOnlyList<PmsHallEventListItemDto> Items { get; set; } = Array.Empty<PmsHallEventListItemDto>();
        public PmsHallBookingsReportSummaryDto Summary { get; set; } = new();
    }

    public sealed class PmsHallBookingsReportSummaryDto
    {
        public int EventCount { get; set; }
        public decimal TotalRent { get; set; }
        public decimal TotalDeposit { get; set; }
        public decimal TotalBalance { get; set; }
    }

    public sealed class PmsHallFinanceReportRowDto
    {
        public int ReservationRouteId { get; set; }
        public int? ReservationZaaerId { get; set; }
        public string ReservationNo { get; set; } = string.Empty;
        public string? HallName { get; set; }
        public string? CustomerName { get; set; }
        public string? OccasionName { get; set; }
        public DateTime? EventDate { get; set; }

        public int DocumentId { get; set; }
        public int? DocumentZaaerId { get; set; }
        public string DocumentNo { get; set; } = string.Empty;
        public DateTime DocumentDate { get; set; }
        public decimal Amount { get; set; }
        public string? Status { get; set; }
        public string? VoucherCode { get; set; }
        public string? VoucherLabel { get; set; }
        public int? OrderId { get; set; }
        public string? ReceiptType { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public string? BankName { get; set; }
        public string? LinkedInvoiceNo { get; set; }
        public int? LinkedInvoiceId { get; set; }
        public int? LinkedInvoiceZaaerId { get; set; }
        public decimal? AmountPaid { get; set; }
        public decimal? AmountRemaining { get; set; }
        public string? CreditType { get; set; }
        public string? ExpenseCategoryName { get; set; }
        public decimal? BeforeTaxAmount { get; set; }
        public decimal? TaxAmount { get; set; }
        public string? ApprovalStatus { get; set; }
        public string? PaymentSource { get; set; }
    }

    public sealed class PmsHallFinanceReportDto
    {
        public IReadOnlyList<PmsHallFinanceReportRowDto> Items { get; set; } = Array.Empty<PmsHallFinanceReportRowDto>();
        public PmsHallFinanceReportSummaryDto Summary { get; set; } = new();
    }

    public sealed class PmsHallFinanceReportSummaryDto
    {
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public sealed class PmsDailyJournalReportDto
    {
        public IReadOnlyList<PmsDailyJournalRowDto> Items { get; set; } = Array.Empty<PmsDailyJournalRowDto>();
        public PmsDailyJournalReportSummaryDto Summary { get; set; } = new();
    }

    public sealed class PmsDailyJournalReportSummaryDto
    {
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public IReadOnlyList<PmsDailyJournalVoucherSummaryDto> VoucherBreakdown { get; set; }
            = Array.Empty<PmsDailyJournalVoucherSummaryDto>();
    }

    public sealed class PmsDailyJournalVoucherSummaryDto
    {
        public string VoucherCode { get; set; } = string.Empty;
        public string VoucherLabel { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public sealed class PmsDailyJournalRowDto
    {
        public int ReceiptId { get; set; }
        public int? ReceiptZaaerId { get; set; }
        public int? OrderId { get; set; }
        public string ReceiptNo { get; set; } = string.Empty;
        public DateTime ReceiptDate { get; set; }
        public string? CustomerName { get; set; }
        public string? ReservationNo { get; set; }
        public int? ReservationZaaerId { get; set; }
        public int ReservationRouteId { get; set; }
        public decimal AmountPaid { get; set; }
        public string? VoucherCode { get; set; }
        public string? VoucherLabel { get; set; }
        public string? PaymentMethod { get; set; }
        public string? ReceiptStatus { get; set; }
    }

    public sealed class PmsNetworkCashPaymentsReportDto
    {
        public IReadOnlyList<PmsDailyJournalRowDto> Items { get; set; } = Array.Empty<PmsDailyJournalRowDto>();
        public PmsNetworkCashPaymentsReportSummaryDto Summary { get; set; } = new();
    }

    public sealed class PmsNetworkCashPaymentsReportSummaryDto
    {
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public IReadOnlyList<PmsPaymentMethodSummaryDto> PaymentMethodBreakdown { get; set; }
            = Array.Empty<PmsPaymentMethodSummaryDto>();
    }

    public sealed class PmsPaymentMethodSummaryDto
    {
        public string PaymentMethodKey { get; set; } = string.Empty;
        public string PaymentMethodLabel { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
