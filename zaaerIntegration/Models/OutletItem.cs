using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
	/// <summary>
	/// جدول عناصر المنافذ - Outlet Items table
	/// Products/items available in outlets (e.g., "كابتشينو", "بيبسي", "كوكا كولا")
	/// </summary>
	[Table("outlet_items")]
	public class OutletItem
	{
		[Key]
		[Column("item_id")]
		public int ItemId { get; set; }

		/// <summary>
		/// Zaaer System ID (معرف Zaaer)
		/// External ID from Zaaer integration system
		/// </summary>
		[Column("zaaer_id")]
		public int? ZaaerId { get; set; }

		[Column("hotel_id")]
		[Required]
		public int HotelId { get; set; }

		[Column("outlet_id")]
		public int? OutletId { get; set; }

		[Column("category_id")]
		public int? CategoryId { get; set; }

		[Column("item_code")]
		[MaxLength(50)]
		public string? ItemCode { get; set; }

		[Column("item_name")]
		[Required]
		[MaxLength(200)]
		public string ItemName { get; set; } = string.Empty;

		[Column("item_name_ar")]
		[MaxLength(200)]
		public string? ItemNameAr { get; set; }

		[Column("description")]
		[MaxLength(1000)]
		public string? Description { get; set; }

		[Column("price", TypeName = "decimal(12,2)")]
		[Required]
		public decimal Price { get; set; } = 0.00M;

		[Column("quantity")]
		public int? Quantity { get; set; }

		[Column("image_url")]
		[MaxLength(500)]
		public string? ImageUrl { get; set; }

		[Column("includes_tax")]
		public bool IncludesTax { get; set; } = false;

		[Column("is_active")]
		public bool IsActive { get; set; } = true;

		[Column("created_by")]
		public int? CreatedBy { get; set; }

		[Column("created_at")]
		public DateTime CreatedAt { get; set; } = DateTime.Now;

		[Column("updated_at")]
		public DateTime? UpdatedAt { get; set; }

		// Navigation properties
		[ForeignKey(nameof(HotelId))]
		public HotelSettings HotelSettings { get; set; } = null!;

		[ForeignKey(nameof(OutletId))]
		public Outlet? Outlet { get; set; }

		[ForeignKey(nameof(CategoryId))]
		public OutletCategory? OutletCategory { get; set; }

		public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
	}
}

