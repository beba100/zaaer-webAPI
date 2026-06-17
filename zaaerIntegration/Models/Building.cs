using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
	/// <summary>
	/// جدول المباني - Buildings table
	/// </summary>
	[Table("buildings")]
	public class Building
	{
		[Key]
		[Column("building_id")]
		public int BuildingId { get; set; }

		[Column("hotel_id")]
		[Required]
		public int HotelId { get; set; }

		[Column("building_number")]
		[MaxLength(50)]
		public string BuildingNumber { get; set; }

		[Column("building_name")]
		[MaxLength(200)]
		public string BuildingName { get; set; }

		[Column("address")]
		[MaxLength(500)]
		public string Address { get; set; }

		[Column("description")]
		[MaxLength(500)]
		public string? Description { get; set; }

		[Column("is_active")]
		public bool IsActive { get; set; } = true;

		/// <summary>
		/// Zaaer System ID (معرف Zaaer)
		/// External ID from Zaaer integration system
		/// </summary>
		[Column("zaaer_id")]
		public int? ZaaerId { get; set; }

        // Navigation properties
		[ForeignKey("HotelId")]
		public HotelSettings HotelSettings { get; set; } = null!;

		public ICollection<Floor> Floors { get; set; } = new List<Floor>();
		public ICollection<Apartment> Apartments { get; set; } = new List<Apartment>();
	}
}

