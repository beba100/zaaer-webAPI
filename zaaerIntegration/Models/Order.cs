using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
	/// <summary>
	/// جدول الطلبات - Orders table
	/// Used for POS/Order management system
	/// </summary>
	[Table("orders")]
	public class Order
	{
		[Key]
		[Column("order_id")]
		public int OrderId { get; set; }

		/// <summary>
		/// Zaaer System ID (معرف Zaaer)
		/// External ID from Zaaer integration system
		/// </summary>
		[Column("zaaer_id")]
		public int? ZaaerId { get; set; }

		[Column("order_no")]
		[Required]
		[MaxLength(50)]
		public string OrderNo { get; set; } = string.Empty;

		[Column("hotel_id")]
		[Required]
		public int HotelId { get; set; }

		[Column("outlet_id")]
		public int? OutletId { get; set; }

		[Column("table_id")]
		public int? TableId { get; set; }

		[Column("customer_id")]
		public int? CustomerId { get; set; }

		[Column("reservation_id")]
		public int? ReservationId { get; set; }

		[Column("order_date")]
		public DateTime OrderDate { get; set; } = DateTime.Now;

		[Column("order_time")]
		[MaxLength(20)]
		public string? OrderTime { get; set; }

		/// <summary>
		/// Order Status: Created, Confirmed, Completed, Cancelled, Refunded
		/// حالة الطلب
		/// </summary>
		[Column("order_status")]
		[MaxLength(50)]
		public string OrderStatus { get; set; } = "Created";

		/// <summary>
		/// Payment Status: Unpaid, Paid, Partial, Refunded
		/// حالة الدفع
		/// </summary>
		[Column("payment_status")]
		[MaxLength(50)]
		public string PaymentStatus { get; set; } = "Unpaid";

		/// <summary>
		/// Order Type: InPlace, ForReservation, Other
		/// نوع الطلب: في المكان، للحجز، أخرى
		/// </summary>
		[Column("order_type")]
		[MaxLength(50)]
		public string OrderType { get; set; } = "InPlace";

		[Column("subtotal", TypeName = "decimal(12,2)")]
		public decimal? Subtotal { get; set; }

		[Column("tax_amount", TypeName = "decimal(12,2)")]
		public decimal? TaxAmount { get; set; }

		[Column("discount_amount", TypeName = "decimal(12,2)")]
		public decimal? DiscountAmount { get; set; }

		[Column("total_amount", TypeName = "decimal(12,2)")]
		public decimal? TotalAmount { get; set; }

		[Column("paid_amount", TypeName = "decimal(12,2)")]
		public decimal PaidAmount { get; set; } = 0.00M;

		[Column("balance", TypeName = "decimal(12,2)")]
		public decimal? Balance { get; set; }

		[Column("target")]
		[MaxLength(500)]
		public string? Target { get; set; }

		[Column("notes")]
		[MaxLength(1000)]
		public string? Notes { get; set; }

		[Column("cancellation_date")]
		public DateTime? CancellationDate { get; set; }

		[Column("cancellation_reason")]
		[MaxLength(500)]
		public string? CancellationReason { get; set; }

		[Column("is_refunded")]
		public bool IsRefunded { get; set; } = false;

		[Column("created_by")]
		public int? CreatedBy { get; set; }

		[Column("created_at")]
		public DateTime CreatedAt { get; set; } = DateTime.Now;

		[Column("updated_at")]
		public DateTime? UpdatedAt { get; set; }

		// Navigation properties
		[ForeignKey(nameof(HotelId))]
		public HotelSettings HotelSettings { get; set; } = null!;

		[ForeignKey(nameof(CustomerId))]
		public Customer? Customer { get; set; }

		public Reservation? Reservation { get; set; }

		public Outlet? Outlet { get; set; }

		public OutletTable? OutletTable { get; set; }

		public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

		public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

		public ICollection<PaymentReceipt> PaymentReceipts { get; set; } = new List<PaymentReceipt>();

		public ICollection<CreditNote> CreditNotes { get; set; } = new List<CreditNote>();
	}
}

