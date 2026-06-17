using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
	/// <summary>
	/// جدول طاولات المنافذ - Outlet Tables table
	/// Tables/seating areas in outlets
	/// </summary>
	[Table("outlet_tables")]
	public class OutletTable
	{
		[Key]
		[Column("table_id")]
		public int TableId { get; set; }

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

		[Column("table_name")]
		[Required]
		[MaxLength(200)]
		public string TableName { get; set; } = string.Empty;

		[Column("table_name_ar")]
		[MaxLength(200)]
		public string? TableNameAr { get; set; }

		[Column("description")]
		[MaxLength(1000)]
		public string? Description { get; set; }

		[Column("capacity")]
		public int? Capacity { get; set; }

		/// <summary>
		/// Status: Available, Reserved, Occupied
		/// حالة الطاولة: متاح، محجوز، مشغول
		/// </summary>
		[Column("status")]
		[MaxLength(50)]
		public string Status { get; set; } = "Available";

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

		public ICollection<Order> Orders { get; set; } = new List<Order>();
	}
}

