#pragma warning disable CS1591

using System.ComponentModel.DataAnnotations;

namespace zaaerIntegration.DTOs.Pms
{
    public sealed class PmsResortTicketTypeDto
    {
        public int TicketTypeId { get; set; }
        public int? ZaaerId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string? NameEn { get; set; }
        public string? Description { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal VatRate { get; set; }
        public int ValidForHours { get; set; }
        public int ValidForMinutes { get; set; }
        public string ValidityMode { get; set; } = "business_day";
        public string TicketCategory { get; set; } = "other";
        public int SortOrder { get; set; }
        public bool IsGeneric { get; set; }
        public bool IsActive { get; set; }
    }

    public sealed class PmsUpsertResortTicketTypeDto
    {
        [Required]
        [StringLength(100)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string NameAr { get; set; } = string.Empty;

        [StringLength(200)]
        public string? NameEn { get; set; }

        public string? Description { get; set; }

        [Range(0, double.MaxValue)]
        public decimal UnitPrice { get; set; }

        [Range(0, 100)]
        public decimal VatRate { get; set; } = 15m;

        [Range(1, 8760)]
        public int ValidForHours { get; set; } = 24;

        [Range(1, 10080)]
        public int ValidForMinutes { get; set; }

        [StringLength(30)]
        public string? ValidityMode { get; set; }

        [StringLength(50)]
        public string TicketCategory { get; set; } = "other";

        public int SortOrder { get; set; }

        public bool IsGeneric { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public sealed class PmsSetResortTicketTypeActiveDto
    {
        public bool IsActive { get; set; }
    }

    public sealed class PmsResortTicketLookupItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
    }

    public sealed class PmsResortTicketPaymentMethodDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string? Code { get; set; }
    }

    public sealed class PmsResortTicketBankDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
    }

    public sealed class PmsResortTicketPricingTaxDto
    {
        public decimal VatRate { get; set; }
        public bool VatTaxIncluded { get; set; }
    }

    public sealed class PmsResortTicketBusinessConfigDto
    {
        public string IssueStartTime { get; set; } = "16:00";
        public string TicketValidityEndTime { get; set; } = "04:00";
        public string? GamesValidityEndTime { get; set; }
        public string DailyCloseTime { get; set; } = "04:00";
        public bool CanIssueNow { get; set; }
        public DateTime? CurrentBusinessServiceDate { get; set; }
    }

    public sealed class PmsUpsertResortTicketBusinessConfigDto
    {
        [Required]
        public string IssueStartTime { get; set; } = "16:00";

        [Required]
        public string TicketValidityEndTime { get; set; } = "04:00";

        public string? GamesValidityEndTime { get; set; }

        [Required]
        public string DailyCloseTime { get; set; } = "04:00";
    }

    public sealed class PmsResortTicketLookupsDto
    {
        public bool IsResort { get; set; }
        public IReadOnlyList<PmsResortTicketLookupItemDto> TicketCategories { get; set; } = Array.Empty<PmsResortTicketLookupItemDto>();
        public IReadOnlyList<PmsResortTicketLookupItemDto> OrderStatuses { get; set; } = Array.Empty<PmsResortTicketLookupItemDto>();
        public IReadOnlyList<PmsResortTicketLookupItemDto> PaymentStatuses { get; set; } = Array.Empty<PmsResortTicketLookupItemDto>();
        public IReadOnlyList<PmsResortTicketPaymentMethodDto> PaymentMethods { get; set; } = Array.Empty<PmsResortTicketPaymentMethodDto>();
        public IReadOnlyList<PmsResortTicketBankDto> Banks { get; set; } = Array.Empty<PmsResortTicketBankDto>();
        public PmsResortTicketPricingTaxDto? PricingTax { get; set; }
        public PmsResortTicketBusinessConfigDto? BusinessConfig { get; set; }
    }

    public sealed class PmsResortTicketPendingInvoiceOrderDto
    {
        public int TicketOrderId { get; set; }
        public string OrderNo { get; set; } = string.Empty;
        public DateTime ServiceDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string? ReceiptNo { get; set; }
        public int? ReceiptId { get; set; }
        public int TicketCount { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
    }

    public sealed class PmsCreateResortTicketInvoicesDto
    {
        [MinLength(1)]
        public List<int> TicketOrderIds { get; set; } = new();
    }

    public sealed class PmsResortTicketReceiptListItemDto
    {
        public int ReceiptId { get; set; }
        public string ReceiptNo { get; set; } = string.Empty;
        public DateTime ReceiptDate { get; set; }
        public decimal AmountPaid { get; set; }
        public string ReceiptType { get; set; } = string.Empty;
        public string? PaymentMethod { get; set; }
        public string ReceiptStatus { get; set; } = string.Empty;
        public int TicketOrderId { get; set; }
        public string OrderNo { get; set; } = string.Empty;
        public DateTime ServiceDate { get; set; }
        public int? InvoiceId { get; set; }
        public string? InvoiceNo { get; set; }
        public bool HasInvoice { get; set; }
        public string? OrderPaymentStatus { get; set; }
    }

    public sealed class PmsResortTicketFinanceReconciliationDto
    {
        public int PendingInvoiceOrderCount { get; set; }
        public decimal PendingInvoiceOrderTotal { get; set; }
        public decimal CollectionReceiptsTotal { get; set; }
        public int CollectionReceiptCount { get; set; }
        public decimal DisbursementReceiptsTotal { get; set; }
        public int DisbursementReceiptCount { get; set; }
        public decimal NetReceiptsTotal { get; set; }
        public decimal InvoicedTotal { get; set; }
        public int InvoicedCount { get; set; }
        /// <summary>Pending order totals minus collection receipts (expect ~0 when all paid sales have matching receipts).</summary>
        public decimal PendingVsCollectionVariance { get; set; }
        public bool IsBalanced { get; set; }
    }

    public sealed class PmsResortTicketInvoiceListItemDto
    {
        public int InvoiceId { get; set; }
        public int? InvoiceZaaerId { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public int? TicketOrderId { get; set; }
        public string? OrderNo { get; set; }
        public DateTime InvoiceDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public string? ZatcaStatus { get; set; }
        public bool SentToZatca { get; set; }
        public string? CreditNoteNo { get; set; }
        public int? CreditNoteId { get; set; }
        public string? CreditNoteZatcaStatus { get; set; }
        public bool CreditNoteSentToZatca { get; set; }
    }

    public sealed class PmsCreateResortTicketOrderLineDto
    {
        [Required]
        public int TicketTypeId { get; set; }

        [Range(1, 500)]
        public int Quantity { get; set; } = 1;
    }

    public sealed class PmsCreateResortTicketOrderDto
    {
        public int? ReservationId { get; set; }
        public int? UnitId { get; set; }
        public int? CustomerId { get; set; }
        public DateTime? ServiceDate { get; set; }
        public bool PayNow { get; set; }
        public int? PaymentMethodId { get; set; }
        public int? BankId { get; set; }
        public string? TransactionNo { get; set; }
        public string? Notes { get; set; }

        [MinLength(1)]
        public List<PmsCreateResortTicketOrderLineDto> Lines { get; set; } = new();
    }

    public sealed class PmsCancelResortTicketOrderDto
    {
        [Required]
        [StringLength(500)]
        public string Reason { get; set; } = string.Empty;

        /// <summary>Required when cancelling a paid order.</summary>
        public bool ConfirmPaidRefund { get; set; }
    }

    public sealed class PmsResortTicketOrderFinancialDto
    {
        public string? InvoiceNo { get; set; }
        public int? InvoiceZaaerId { get; set; }
        public string? InvoicePaymentStatus { get; set; }
        public bool InvoiceSentToZatca { get; set; }
        public string? InvoiceZatcaStatus { get; set; }
        public string? ReceiptNo { get; set; }
        public string? ReceiptStatus { get; set; }
        public string? RefundReceiptNo { get; set; }
        public int? RefundReceiptId { get; set; }
        public string? CreditNoteNo { get; set; }
        public int? CreditNoteId { get; set; }
        public bool CanCancel { get; set; }
        public string? CancelBlockReason { get; set; }
        public int IssuedTicketCount { get; set; }
        public int UsedTicketCount { get; set; }
        public int CancelledTicketCount { get; set; }
        public bool WillCreateRefundDisbursement { get; set; }
        public bool WillCreateCreditNote { get; set; }
        public bool WillReverseInvoiceOnly { get; set; }
    }

    public sealed class PmsResortTicketDto
    {
        public int TicketId { get; set; }
        public int? ZaaerId { get; set; }
        public int TicketOrderId { get; set; }
        public int TicketTypeId { get; set; }
        public string TicketTypeName { get; set; } = string.Empty;
        public string TicketNo { get; set; } = string.Empty;
        public string QrCode { get; set; } = string.Empty;
        public string TicketStatus { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal VatAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
        public DateTime? PrintedAt { get; set; }
        public DateTime? UsedAt { get; set; }
        public DateTime? SessionStartedAt { get; set; }
        public string? ValidityMode { get; set; }
        public DateTime? CancelledAt { get; set; }
    }

    public sealed class PmsResortTicketOrderDto
    {
        public int TicketOrderId { get; set; }
        public int? ZaaerId { get; set; }
        public string OrderNo { get; set; } = string.Empty;
        public int? ReservationId { get; set; }
        public int? UnitId { get; set; }
        public int? CustomerId { get; set; }
        public int? InvoiceId { get; set; }
        public int? ReceiptId { get; set; }
        public int? RefundReceiptId { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime ServiceDate { get; set; }
        public decimal Subtotal { get; set; }
        public decimal VatAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public string OrderStatus { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string? CancelReason { get; set; }
        public PmsResortTicketOrderFinancialDto? Financial { get; set; }
        public List<PmsResortTicketDto> Tickets { get; set; } = new();
    }

    public sealed class PmsResortTicketPrintDto
    {
        public int TicketOrderId { get; set; }
        public string OrderNo { get; set; } = string.Empty;
        public string HotelName { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public DateTime ServiceDate { get; set; }
        public decimal TotalAmount { get; set; }
        public IReadOnlyList<PmsResortTicketDto> Tickets { get; set; } = Array.Empty<PmsResortTicketDto>();
    }

    public sealed class PmsRedeemResortTicketDto
    {
        [Required]
        [StringLength(256)]
        public string QrCode { get; set; } = string.Empty;

        /// <summary>Ticket type code or category shortcut (entry, games, pool) for station binding.</summary>
        [StringLength(100)]
        public string? StationCode { get; set; }
    }

    public sealed class PmsResortTicketRedeemResultDto
    {
        public bool Success { get; set; }
        public string? BlockReason { get; set; }
        public PmsResortTicketDto? Ticket { get; set; }
        public string? OrderNo { get; set; }
        public string? OrderStatus { get; set; }
        public DateTime? RedeemedAt { get; set; }
        public bool IsReentry { get; set; }
        /// <summary>valid | expired | not_yet_valid | pending_activation</summary>
        public string? ValidityStatus { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        /// <summary>Minutes left in an active from_first_scan session (null if not started or expired).</summary>
        public int? RemainingMinutes { get; set; }
    }
}
