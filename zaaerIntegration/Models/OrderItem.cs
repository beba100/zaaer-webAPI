using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
	/// <summary>
	/// جدول عناصر الطلب - Order Items table
	/// Contains line items for each order
	/// </summary>
	[Table("order_items")]
	public class OrderItem
	{
		[Key]
		[Column("order_item_id")]
		public int OrderItemId { get; set; }

		/// <summary>
		/// Zaaer System ID (معرف Zaaer)
		/// External ID from Zaaer integration system
		/// </summary>
		[Column("zaaer_id")]
		public int? ZaaerId { get; set; }

		[Column("order_id")]
		[Required]
		public int OrderId { get; set; }

		[Column("item_id")]
		public int? ItemId { get; set; }

		[Column("item_name")]
		[Required]
		[MaxLength(200)]
		public string ItemName { get; set; } = string.Empty;

		[Column("quantity")]
		[Required]
		public int Quantity { get; set; }

		[Column("unit_price", TypeName = "decimal(12,2)")]
		[Required]
		public decimal UnitPrice { get; set; }

		[Column("discount", TypeName = "decimal(12,2)")]
		public decimal Discount { get; set; } = 0.00M;

		[Column("total_price", TypeName = "decimal(12,2)")]
		[Required]
		public decimal TotalPrice { get; set; }

		[Column("created_at")]
		public DateTime CreatedAt { get; set; } = DateTime.Now;

		// Navigation properties
		[ForeignKey(nameof(OrderId))]
		public Order Order { get; set; } = null!;

		[ForeignKey(nameof(ItemId))]
		public OutletItem? OutletItem { get; set; }
	}
}

