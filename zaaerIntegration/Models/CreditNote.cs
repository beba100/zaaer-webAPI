using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
	/// <summary>
	/// Credit Note Entity - إشعار دائن (عكس الفاتورة)
	/// Used to reverse/cancel invoices without actual cash refund
	/// </summary>
	[Table("credit_notes")]
	public class CreditNote
	{
		[Key]
		[Column("credit_note_id")]
		public int CreditNoteId { get; set; }

		[Column("credit_note_no")]
		[Required]
		[MaxLength(50)]
		public string CreditNoteNo { get; set; } = string.Empty;

		[Column("hotel_id")]
		[Required]
		public int HotelId { get; set; }

		/// <summary>
		/// Original Invoice to be reversed
		/// الفاتورة الأصلية المراد عكسها
		/// IMPORTANT: This contains invoices.zaaer_id (NOT invoices.invoice_id)
		/// The relationship is: credit_notes.invoice_id = invoices.zaaer_id
		/// </summary>
		[Column("invoice_id")]
		[Required]
		public int InvoiceId { get; set; } // Contains zaaer_id from invoices table

	[Column("reservation_id")]
	public int? ReservationId { get; set; }

	[Column("order_id")]
	public int? OrderId { get; set; }

	[Column("customer_id")]
	public int? CustomerId { get; set; }

	[Column("credit_note_date")]
	public DateTime CreditNoteDate { get; set; } = DateTime.Now;

	/// <summary>
	/// Credit Note Date in Hijri
	/// </summary>
	[Column("credit_note_date_hijri")]
	[MaxLength(20)]
	public string? CreditNoteDateHijri { get; set; }

	/// <summary>
	/// Subtotal amount before tax
	/// المبلغ الإجمالي قبل الضريبة
	/// </summary>
	[Column("subtotal", TypeName = "decimal(12,2)")]
	public decimal? Subtotal { get; set; }

	/// <summary>
	/// VAT Rate percentage
	/// نسبة ضريبة القيمة المضافة
	/// </summary>
	[Column("vat_rate", TypeName = "decimal(12,4)")]
	public decimal? VatRate { get; set; }

	/// <summary>
	/// VAT Amount
	/// مبلغ ضريبة القيمة المضافة
	/// </summary>
	[Column("vat_amount", TypeName = "decimal(12,2)")]
	public decimal? VatAmount { get; set; }

	/// <summary>
	/// Lodging Tax Rate percentage
	/// نسبة ضريبة الإقامة
	/// </summary>
	[Column("lodging_tax_rate", TypeName = "decimal(12,4)")]
	public decimal? LodgingTaxRate { get; set; }

	/// <summary>
	/// Lodging Tax Amount
	/// مبلغ ضريبة الإقامة
	/// </summary>
	[Column("lodging_tax_amount", TypeName = "decimal(12,2)")]
	public decimal? LodgingTaxAmount { get; set; }

	/// <summary>
	/// Amount to be credited (reversed from invoice)
	/// المبلغ المراد عكسه من الفاتورة
	/// </summary>
	[Column("credit_amount", TypeName = "decimal(12,2)")]
	[Required]
	public decimal CreditAmount { get; set; }

		[Column("reason")]
		[Required]
		[MaxLength(500)]
		public string Reason { get; set; } = string.Empty;

		/// <summary>
		/// Credit Note Type (e.g., "refund", "discount", "adjustment", "cancellation")
		/// نوع الإشعار الدائن
		/// </summary>
		[Column("credit_type")]
		[MaxLength(50)]
		public string CreditType { get; set; } = "refund";

		[Column("notes")]
		[MaxLength(1000)]
		public string? Notes { get; set; }

		/// <summary>
		/// ZATCA Integration
		/// </summary>
		[Column("is_sent_zatca")]
		public bool IsSentZatca { get; set; } = false;

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

		[Column("created_by")]
		public int? CreatedBy { get; set; }

		[Column("created_at")]
		public DateTime CreatedAt { get; set; } = DateTime.Now;

	/// <summary>
	/// Zaaer System ID (معرف Zaaer)
	/// External ID from Zaaer integration system
	/// </summary>
	[Column("zaaer_id")]
	public int? ZaaerId { get; set; }

	/// <summary>
	/// VoM Sync Status (حالة المزامنة مع VoM)
	/// Values: 'pending', 'sent', 'failed'
	/// </summary>
	[Column("status_vom")]
	[MaxLength(20)]
	public string StatusVoM { get; set; } = "pending";

	/// <summary>
	/// VoM Payload (البيانات المرسلة إلى VoM)
	/// JSON payload sent to VoM for audit/retry
	/// </summary>
	[Column("vom_payload")]
	public string? VomPayload { get; set; }

	/// <summary>
	/// VoM Sent At (تاريخ الإرسال إلى VoM)
	/// </summary>
	[Column("vom_sent_at")]
	public DateTime? VomSentAt { get; set; }

	/// <summary>
	/// VoM Error (خطأ VoM)
	/// </summary>
	[Column("vom_error")]
	public string? VomError { get; set; }

	/// <summary>
	/// VoM Retry Count (عدد محاولات الإرسال)
	/// </summary>
	[Column("vom_retry_count")]
	public int VomRetryCount { get; set; } = 0;

	// Navigation properties
	public HotelSettings HotelSettings { get; set; } = null!;
	
	/// <summary>
	/// Invoice navigation property
	/// NOTE: This property is IGNORED in ApplicationDbContext because:
	/// - CreditNote.InvoiceId contains invoices.zaaer_id (NOT invoices.invoice_id)
	/// - Cannot use standard FK constraint (see ConfigureCreditNoteRelationships)
	/// - To get the invoice, use: invoices WHERE zaaer_id = creditNote.InvoiceId
	/// </summary>
	public Invoice Invoice { get; set; } = null!;
	
	public Reservation? Reservation { get; set; }
	[ForeignKey(nameof(OrderId))]
	public Order? Order { get; set; }
	public ICollection<CustomerTransaction> CustomerTransactions { get; set; } = new List<CustomerTransaction>();
	}
}

