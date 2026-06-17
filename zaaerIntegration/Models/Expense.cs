using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using zaaerIntegration.Models;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
	/// <summary>
	/// Expense vouchers table
	/// </summary>
	[Table("expenses")]
	public class Expense
	{
		/// <summary>
		/// Global integration id from Master numbering (EntityZaaerCounters / GetNextBusinessIdentity).
		/// Stored in expense_id — manual insert, NOT SQL Server IDENTITY.
		/// </summary>
		[Key]
		[Column("expense_id")]
		public long ExpenseId { get; set; }

		/// <summary>Legacy counter — SQL IDENTITY on most tenant DBs; do not set on insert.</summary>
		[Column("old_expense_id")]
		public int OldExpenseId { get; set; }

		[Column("local_expense_id")]
		public int LocalExpenseId { get; set; }

		[Column("expense_seq")]
		public int ExpenseSeq { get; set; }

		[Column("expense_no")]
		[MaxLength(20)]
		public string ExpenseNo { get; set; } = string.Empty;

	[Column("date_time")]
	public DateTime DateTime { get; set; }

	[Column("due_date")]
	public DateTime? DueDate { get; set; }

	[Column("comment")]
	[MaxLength(500)]
	public string? Comment { get; set; }

        [Column("hotel_id")]
        public int HotelId { get; set; }

        [Column("expense_category_id")]
        public int? ExpenseCategoryId { get; set; }

        [Column("tax_rate", TypeName = "decimal(5,2)")]
        public decimal? TaxRate { get; set; }

        [Column("tax_amount", TypeName = "decimal(12,2)")]
        public decimal? TaxAmount { get; set; }

        [Column("before_tax_amount", TypeName = "decimal(12,2)")]
        public decimal? BeforeTaxAmount { get; set; }

        [Column("total_amount", TypeName = "decimal(12,2)")]
        [Required]
        public decimal TotalAmount { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// معرف المستخدم الذي قام بتحديث المصروف
        /// User ID who updated the expense
        /// </summary>
        [Column("updated_by")]
        public int? UpdatedBy { get; set; }

        /// <summary>
        /// حالة الموافقة على المصروف
        /// Approval status: auto-approved, pending, accepted, rejected, awaiting-manager
        /// </summary>
        [Column("approval_status")]
        [MaxLength(30)]
        public string ApprovalStatus { get; set; } = "auto-approved";

        /// <summary>
        /// معرف المستخدم الذي وافق/رفض المصروف
        /// User ID who approved/rejected the expense
        /// </summary>
        [Column("approved_by")]
        public int? ApprovedBy { get; set; }

        /// <summary>
        /// تاريخ ووقت الموافقة/الرفض
        /// Date and time of approval/rejection
        /// </summary>
        [Column("approved_at")]
        public DateTime? ApprovedAt { get; set; }

        /// <summary>
        /// سبب الرفض (في حالة رفض المصروف)
        /// Rejection reason (if expense is rejected)
        /// </summary>
        [Column("rejection_reason")]
        [MaxLength(500)]
        public string? RejectionReason { get; set; }

        /// <summary>
        /// مصدر السداد: Branch (صندوق الفرع) أو Management (الإدارة)
        /// Payment Source: Branch or Management
        /// </summary>
        [Column("payment_source")]
        [MaxLength(20)]
        public string? PaymentSource { get; set; }

        // ================================================================
        // VoM Integration Fields
        // ================================================================

        /// <summary>
        /// حالة إرسال المصروف إلى VoM: pending, sent, failed
        /// VoM sync status: pending, sent, failed
        /// </summary>
        [Column("status_vom")]
        [MaxLength(20)]
        public string? StatusVoM { get; set; } = "pending";

        /// <summary>
        /// البيانات المرسلة إلى VoM (JSON payload)
        /// VoM payload sent
        /// </summary>
        [Column("vom_payload")]
        public string? VomPayload { get; set; }

        /// <summary>
        /// تاريخ إرسال المصروف إلى VoM
        /// VoM sent date
        /// </summary>
        [Column("vom_sent_at")]
        public DateTime? VomSentAt { get; set; }

        /// <summary>
        /// رسالة الخطأ من VoM (في حالة الفشل)
        /// VoM error message (if failed)
        /// </summary>
        [Column("vom_error")]
        public string? VomError { get; set; }

        /// <summary>
        /// عدد محاولات الإرسال إلى VoM
        /// VoM retry count
        /// </summary>
        [Column("vom_retry_count")]
        public int? VomRetryCount { get; set; } = 0;

        // Navigation properties
        [ForeignKey("HotelId")]
        public HotelSettings? HotelSettings { get; set; }

        // ✅ ExpenseCategoryId refers to Master DB ExpenseCategories, NOT Tenant DB
        // ⚠️ Cannot use [ForeignKey] attribute here because ExpenseCategory is in Master DB
        // This is just a reference ID, not a real Foreign Key relationship
        // public ExpenseCategory? ExpenseCategory { get; set; }

        /// <summary>
        /// قائمة الغرف المرتبطة بهذه النفقة
        /// List of rooms/apartments associated with this expense
        /// </summary>
        public ICollection<ExpenseRoom> ExpenseRooms { get; set; } = new List<ExpenseRoom>();

        /// <summary>
        /// قائمة الصور المرتبطة بهذه النفقة
        /// List of images associated with this expense
        /// </summary>
        public ICollection<ExpenseImage> ExpenseImages { get; set; } = new List<ExpenseImage>();

        /// <summary>
        /// بيانات الشركة الموردة (عند وجود ضريبة)
        /// Supplier company details when expense has VAT invoice
        /// </summary>
        public ExpenseCompany? ExpenseCompany { get; set; }
    }
}


