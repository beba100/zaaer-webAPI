using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
	/// <summary>
	/// جدول سندات القبض - Payment receipts table
	/// </summary>
	[Table("payment_receipts")]
	public class PaymentReceipt
	{
		[Key]
		[Column("receipt_id")]
		public int ReceiptId { get; set; }

		[Column("receipt_no")]
		[Required]
		[MaxLength(50)]
		public string ReceiptNo { get; set; }

		[Column("hotel_id")]
		[Required]
		public int HotelId { get; set; }

		[Column("reservation_id")]
		public int? ReservationId { get; set; }

		[Column("unit_id")]
		public int? UnitId { get; set; }

	[Column("invoice_id")]
	public int? InvoiceId { get; set; }

	[Column("order_id")]
	public int? OrderId { get; set; }

	[Column("customer_id")]
	public int? CustomerId { get; set; }

	[Column("receipt_date")]
	public DateTime ReceiptDate { get; set; } = DateTime.Now;

	/// <summary>
	/// Receipt Type: receipt | security_deposit
	/// نوع السند: سند قبض عادي | سند قبض تأمين
	/// </summary>
	[Column("receipt_type")]
	[MaxLength(50)]
	public string ReceiptType { get; set; } = "receipt";

	/// <summary>
	/// Voucher code (for discount vouchers)
	/// رمز القسيمة (للخصومات)
	/// </summary>
	[Column("voucher_code")]
	[MaxLength(50)]
	public string? VoucherCode { get; set; }

	[Column("amount_paid", TypeName = "decimal(12,2)")]
	[Required]
	public decimal AmountPaid { get; set; }

	[Column("payment_method_id")]
	public int? PaymentMethodId { get; set; }

	/// <summary>
	/// Legacy: Payment Method as string (for backward compatibility)
	/// قديم: طريقة الدفع كنص
	/// </summary>
	[Column("payment_method")]
	[MaxLength(50)]
	public string? PaymentMethod { get; set; }

		[Column("bank_id")]
		public int? BankId { get; set; }

		/// <summary>
		/// Bank Name (اسم البنك)
		/// Stored directly from Zaaer integration
		/// </summary>
		[Column("bank_name")]
		[MaxLength(255)]
		public string? BankName { get; set; }

		[Column("transaction_no")]
		[MaxLength(100)]
		public string TransactionNo { get; set; }

		[Column("notes")]
		public string Notes { get; set; }

		/// <summary>Business reason line (e.g. rental fees for reservation).</summary>
		[Column("reason")]
		[MaxLength(500)]
		public string? Reason { get; set; }

		/// <summary>Rent period start (date only).</summary>
		[Column("receipt_from", TypeName = "date")]
		public DateTime? ReceiptFrom { get; set; }

		/// <summary>Rent period end (date only).</summary>
		[Column("receipt_to", TypeName = "date")]
		public DateTime? ReceiptTo { get; set; }

		/// <summary>Building guard rent flag (rent receipts only).</summary>
		[Column("is_building_guard_rent")]
		public bool IsBuildingGuardRent { get; set; }

		/// <summary>
		/// Receipt status: active | cancelled
		/// حالة السند: نشط | ملغي
		/// </summary>
		[Column("receipt_status")]
		[MaxLength(50)]
		public string ReceiptStatus { get; set; } = "active";

		/// <summary>PMS operator id (<c>pms_users.user_id</c> from JWT).</summary>
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

	/// <summary>
	/// VoM Reverse Sent Flag (تم إرسال القيد العكسي إلى VoM)
	/// - 0: لم يتم إرسال قيد عكسي
	/// - 1: تم إرسال قيد عكسي بنجاح
	/// </summary>
	[Column("vom_reverse_sent")]
	public bool VomReverseSent { get; set; } = false;

	/// <summary>
	/// VoM Journal Entry ID (معرف القيد المحاسبي في VoM)
	/// يتم حفظه مباشرة من response عند إنشاء القيد في VoM
	/// يستخدم عند التحديث لحذف القيد القديم قبل إنشاء قيد جديد
	/// </summary>
	[Column("vom_journal_entry_id")]
	public int? VomJournalEntryId { get; set; }

		/// <summary>
		/// Total amount allocated to invoices
		/// إجمالي المبلغ المخصص للفواتير
		/// </summary>
		[Column("allocated_amount", TypeName = "decimal(12,2)")]
		public decimal AllocatedAmount { get; set; } = 0.00M;

		/// <summary>
		/// Remaining unallocated amount available for allocation
		/// المبلغ المتبقي غير المخصص المتاح للتخصيص
		/// </summary>
		[Column("unallocated_amount", TypeName = "decimal(12,2)")]
		public decimal? UnallocatedAmount { get; set; }

		/// <summary>
		/// Flag indicating if receipt is fully allocated to invoices
		/// علامة تشير إلى ما إذا كان السند مخصص بالكامل للفواتير
		/// </summary>
		[Column("is_fully_allocated")]
		public bool IsFullyAllocated { get; set; } = false;

		/// <summary>
		/// Revenue Category for VoM integration (إيرادات أخرى)
		/// Determined locally when receipt is created from order
		/// </summary>
		[Column("revenue_category")]
		[MaxLength(50)]
		public string? RevenueCategory { get; set; }

		// Navigation properties
		[ForeignKey(nameof(HotelId))]
		[InverseProperty(nameof(HotelSettings.PaymentReceipts))]
		public HotelSettings HotelSettings { get; set; } = null!;
		public Reservation Reservation { get; set; }
		public ReservationUnit ReservationUnit { get; set; }
		public Invoice Invoice { get; set; }
		[ForeignKey(nameof(OrderId))]
		public Order? Order { get; set; }
		public Models.PaymentMethod? PaymentMethodNavigation { get; set; }
		public Bank? BankNavigation { get; set; }
		public ICollection<CustomerTransaction> CustomerTransactions { get; set; }
		
		/// <summary>
		/// Many-to-Many relationship with invoices through InvoiceReceiptMapping
		/// علاقة Many-to-Many مع الفواتير من خلال InvoiceReceiptMapping
		/// </summary>
		public ICollection<InvoiceReceiptMapping> InvoiceReceiptMappings { get; set; } = new List<InvoiceReceiptMapping>();
	}
}

