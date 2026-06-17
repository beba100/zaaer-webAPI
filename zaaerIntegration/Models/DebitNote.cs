using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Debit note (إشعار مدين) — required for ZATCA compliance samples and B2B adjustments.
    /// </summary>
    [Table("debit_notes")]
    public class DebitNote
    {
        [Key]
        [Column("debit_note_id")]
        public int DebitNoteId { get; set; }

        [Column("debit_note_no")]
        [Required]
        [MaxLength(50)]
        public string DebitNoteNo { get; set; } = string.Empty;

        [Column("hotel_id")]
        public int HotelId { get; set; }

        /// <summary>References invoices.zaaer_id when set, else invoices.invoice_id (same as credit_notes).</summary>
        [Column("invoice_id")]
        public int InvoiceId { get; set; }

        [Column("reservation_id")]
        public int? ReservationId { get; set; }

        [Column("customer_id")]
        public int? CustomerId { get; set; }

        [Column("order_id")]
        public int? OrderId { get; set; }

        [Column("debit_note_date")]
        public DateTime DebitNoteDate { get; set; } = KsaTime.Now;

        [Column("debit_note_date_hijri")]
        [MaxLength(20)]
        public string? DebitNoteDateHijri { get; set; }

        [Column("subtotal", TypeName = "decimal(12,2)")]
        public decimal? Subtotal { get; set; }

        [Column("vat_rate", TypeName = "decimal(12,4)")]
        public decimal? VatRate { get; set; }

        [Column("vat_amount", TypeName = "decimal(12,2)")]
        public decimal? VatAmount { get; set; }

        [Column("lodging_tax_rate", TypeName = "decimal(12,4)")]
        public decimal? LodgingTaxRate { get; set; }

        [Column("lodging_tax_amount", TypeName = "decimal(12,2)")]
        public decimal? LodgingTaxAmount { get; set; }

        [Column("debit_amount", TypeName = "decimal(12,2)")]
        public decimal DebitAmount { get; set; }

        [Column("original_invoice_amount", TypeName = "decimal(12,2)")]
        public decimal? OriginalInvoiceAmount { get; set; }

        [Column("reason")]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        [Column("debit_type")]
        [MaxLength(50)]
        public string DebitType { get; set; } = "adjustment";

        [Column("notes")]
        [MaxLength(1000)]
        public string? Notes { get; set; }

        [Column("is_sent_zatca")]
        public bool IsSentZatca { get; set; }

        [Column("zatca_uuid")]
        [MaxLength(255)]
        public string? ZatcaUuid { get; set; }

        [Column("zatca_status")]
        [MaxLength(30)]
        public string ZatcaStatus { get; set; } = "pending";

        [Column("zatca_icv")]
        public int? ZatcaIcv { get; set; }

        [Column("zatca_hash")]
        [MaxLength(512)]
        public string? ZatcaHash { get; set; }

        [Column("zatca_qr")]
        public string? ZatcaQr { get; set; }

        [Column("zatca_response")]
        public string? ZatcaResponse { get; set; }

        [Column("zatca_profile")]
        [MaxLength(20)]
        public string? ZatcaProfile { get; set; }

        [Column("zatca_submission_mode")]
        [MaxLength(20)]
        public string? ZatcaSubmissionMode { get; set; }

        [Column("zatca_retry_count")]
        public int ZatcaRetryCount { get; set; }

        [Column("zatca_last_error")]
        public string? ZatcaLastError { get; set; }

        [Column("zatca_sent_at")]
        public DateTime? ZatcaSentAt { get; set; }

        [Column("is_compliance_sample")]
        public bool IsComplianceSample { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("zaaer_id")]
        public int? ZaaerId { get; set; }

        public HotelSettings HotelSettings { get; set; } = null!;
        public Reservation? Reservation { get; set; }
        [ForeignKey(nameof(OrderId))]
        public Order? Order { get; set; }
    }
}
