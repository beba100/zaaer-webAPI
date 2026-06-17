using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
	/// <summary>
	/// جدول المنافذ - Outlets table
	/// Represents physical outlets/branches in the hotel (e.g., "ثلاجة", "الإستقبال")
	/// </summary>
	[Table("outlets")]
	public class Outlet
	{
		[Key]
		[Column("outlet_id")]
		public int OutletId { get; set; }

		/// <summary>
		/// Zaaer System ID (معرف Zaaer)
		/// External ID from Zaaer integration system
		/// </summary>
		[Column("zaaer_id")]
		public int? ZaaerId { get; set; }

		[Column("hotel_id")]
		[Required]
		public int HotelId { get; set; }

		[Column("outlet_name")]
		[Required]
		[MaxLength(200)]
		public string OutletName { get; set; } = string.Empty;

		[Column("outlet_name_ar")]
		[MaxLength(200)]
		public string? OutletNameAr { get; set; }

		[Column("location")]
		[MaxLength(500)]
		public string? Location { get; set; }

		[Column("image_url")]
		[MaxLength(500)]
		public string? ImageUrl { get; set; }

		/// <summary>
		/// Status: Open, Closed
		/// حالة المنفذ: مفتوح، مغلق
		/// </summary>
		[Column("status")]
		[MaxLength(50)]
		public string Status { get; set; } = "Open";

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

		public ICollection<OutletItem> OutletItems { get; set; } = new List<OutletItem>();

		public ICollection<OutletTable> OutletTables { get; set; } = new List<OutletTable>();

		public ICollection<Order> Orders { get; set; } = new List<Order>();
	}
}

