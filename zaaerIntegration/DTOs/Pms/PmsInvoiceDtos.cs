#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.Pms
{
    public sealed class PmsInvoiceRowDto
    {
        public int InvoiceId { get; set; }
        public int? ZaaerId { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal? TotalAmount { get; set; }
        public DateTime? PeriodFrom { get; set; }
        public DateTime? PeriodTo { get; set; }
        public string ZatcaStatus { get; set; } = "pending";
        public string? CustomerName { get; set; }
        public int? CustomerId { get; set; }
        public int HotelId { get; set; }
        public int? ReservationId { get; set; }
        public string? Notes { get; set; }
        public string PaymentStatus { get; set; } = "unpaid";
        public bool ParentZatcaSubmitted { get; set; }
        public int RelatedAdjustmentCount { get; set; }
        public int RelatedCreditNoteCount { get; set; }

        /// <summary>Net invoice balance available for credit/debit notes (total − credits + debits).</summary>
        public decimal AdjustmentRemainingAmount { get; set; }

        public string Number => InvoiceNo;
        public DateTime Date => InvoiceDate;
        public decimal? Amount => TotalAmount;
        public string Status => ZatcaStatus;
    }

    public sealed class PmsInvoiceLastInvoiceDto
    {
        public string? InvoiceNo { get; set; }
        public decimal? TotalAmount { get; set; }
        public DateTime? PeriodFrom { get; set; }
        public DateTime? PeriodTo { get; set; }
    }

    public sealed class PmsInvoiceContextDto
    {
        public int ReservationId { get; set; }
        public int HotelId { get; set; }

        /// <summary>Outstanding payment balance on the reservation (rent receipts vs folio).</summary>
        public decimal PaymentBalanceAmount { get; set; }

        /// <summary>Amount still to invoice (folio/paid net minus net invoiced, after credit notes).</summary>
        public decimal InvoiceRemainingAmount { get; set; }

        public decimal GrossInvoicedAmount { get; set; }
        public decimal NetInvoicedAmount { get; set; }
        public decimal CreditNotesTotal { get; set; }
        public decimal InvoiceRequiredAmount { get; set; }

        /// <summary>Legacy field — equals <see cref="InvoiceRemainingAmount"/> for invoice create UI.</summary>
        public decimal BalanceAmount { get; set; }

        public DateTime? DefaultPeriodFrom { get; set; }
        public DateTime? DefaultPeriodTo { get; set; }
        public decimal? VatRate { get; set; }
        public decimal? LodgingTaxRate { get; set; }
        public PmsInvoiceLastInvoiceDto? LastInvoice { get; set; }
    }

    public sealed class PmsCreateInvoiceDto
    {
        public int HotelId { get; set; }
        public int ReservationId { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime? PeriodFrom { get; set; }
        public DateTime? PeriodTo { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public string? Notes { get; set; }
        public int? CreatedBy { get; set; }
    }

    public sealed class PmsCreateCreditNoteDto
    {
        public int HotelId { get; set; }
        public int InvoiceId { get; set; }
        public decimal CreditAmount { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public int? CreatedBy { get; set; }
    }

    public sealed class PmsCreateDebitNoteDto
    {
        public int HotelId { get; set; }
        public int InvoiceId { get; set; }
        public decimal DebitAmount { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public int? CreatedBy { get; set; }
    }

    public sealed class PmsAdjustmentRowDto
    {
        public string Kind { get; set; } = string.Empty;
        public int DocumentId { get; set; }
        public int? ZaaerId { get; set; }
        public string DocumentNo { get; set; } = string.Empty;
        public DateTime DocumentDate { get; set; }
        public decimal Amount { get; set; }
        public string ZatcaStatus { get; set; } = "pending";
        public string? Reason { get; set; }
    }

    public sealed class PmsCreditNoteReservationRowDto
    {
        public int CreditNoteId { get; set; }
        public int? ZaaerId { get; set; }
        public string CreditNoteNo { get; set; } = string.Empty;
        public DateTime CreditNoteDate { get; set; }
        public decimal CreditAmount { get; set; }
        public int InvoiceId { get; set; }
        public string? InvoiceNo { get; set; }
        public int? InvoiceZaaerId { get; set; }
        public string ZatcaStatus { get; set; } = "pending";
        public string? CreditType { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public int? ReservationId { get; set; }
        public int HotelId { get; set; }
    }

    public sealed class PmsZatcaSendDocumentRequestDto
    {
        public string DocumentKind { get; set; } = string.Empty;
        public int DocumentId { get; set; }
    }

    public sealed class PmsZatcaSendDocumentResultDto
    {
        public bool Success { get; set; }
        public string? ZatcaStatus { get; set; }
        public string? Message { get; set; }
    }
}
