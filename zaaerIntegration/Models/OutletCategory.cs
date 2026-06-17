using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
	/// <summary>
	/// جدول فئات المنافذ - Outlet Categories table
	/// Categories for organizing outlet items (e.g., "مشروبات باردة", "مشروبات ساخنة")
	/// </summary>
	[Table("outlet_categories")]
	public class OutletCategory
	{
		[Key]
		[Column("category_id")]
		public int CategoryId { get; set; }

		/// <summary>
		/// Zaaer System ID (معرف Zaaer)
		/// External ID from Zaaer integration system
		/// </summary>
		[Column("zaaer_id")]
		public int? ZaaerId { get; set; }

		[Column("hotel_id")]
		[Required]
		public int HotelId { get; set; }

		[Column("category_name")]
		[Required]
		[MaxLength(200)]
		public string CategoryName { get; set; } = string.Empty;

		[Column("category_name_ar")]
		[MaxLength(200)]
		public string? CategoryNameAr { get; set; }

		[Column("description")]
		[MaxLength(1000)]
		public string? Description { get; set; }

		[Column("sort_order")]
		public int SortOrder { get; set; } = 0;

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
	}
}

