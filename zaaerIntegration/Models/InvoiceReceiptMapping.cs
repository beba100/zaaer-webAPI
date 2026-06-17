using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
	/// <summary>
	/// جدول ربط الفواتير بسندات القبض - Invoice Receipt Mapping table
	/// Many-to-Many relationship between invoices and payment receipts
	/// </summary>
	[Table("invoice_receipt_mappings")]
	public class InvoiceReceiptMapping
	{
		/// <summary>
		/// Mapping ID (Primary Key)
		/// معرف الربط
		/// </summary>
		[Key]
		[Column("mapping_id")]
		public int MappingId { get; set; }

		/// <summary>
		/// Invoice ID (Foreign Key)
		/// معرف الفاتورة
		/// </summary>
		[Column("invoice_id")]
		[Required]
		public int InvoiceId { get; set; }

		/// <summary>
		/// Receipt ID (Foreign Key)
		/// معرف السند
		/// </summary>
		[Column("receipt_id")]
		[Required]
		public int ReceiptId { get; set; }

		/// <summary>
		/// Amount allocated from this receipt to this invoice
		/// المبلغ المخصص من هذا السند لهذه الفاتورة
		/// </summary>
		[Column("allocated_amount", TypeName = "decimal(12,2)")]
		[Required]
		public decimal AllocatedAmount { get; set; }

		/// <summary>
		/// Date when this mapping was created
		/// تاريخ إنشاء هذا الربط
		/// </summary>
		[Column("mapping_date")]
		[Required]
		public DateTime MappingDate { get; set; } = DateTime.Now;

		/// <summary>
		/// User who created this mapping
		/// المستخدم الذي أنشأ هذا الربط
		/// </summary>
		[Column("created_by")]
		public int? CreatedBy { get; set; }

		/// <summary>
		/// Creation timestamp
		/// وقت الإنشاء
		/// </summary>
		[Column("created_at")]
		public DateTime CreatedAt { get; set; } = DateTime.Now;

		// Navigation properties
		/// <summary>
		/// Navigation property to Invoice
		/// خاصية التنقل للفاتورة
		/// </summary>
		[ForeignKey(nameof(InvoiceId))]
		public Invoice? Invoice { get; set; }

		/// <summary>
		/// Navigation property to PaymentReceipt
		/// خاصية التنقل لسند القبض
		/// </summary>
		[ForeignKey(nameof(ReceiptId))]
		public PaymentReceipt? PaymentReceipt { get; set; }
	}
}
