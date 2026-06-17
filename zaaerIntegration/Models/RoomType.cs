using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
	/// <summary>
	/// جدول أنواع الغرف - Room types table
	/// </summary>
	[Table("room_types")]
	public class RoomType
	{
		[Key]
		[Column("roomtype_id")]
		public int RoomTypeId { get; set; }

		[Column("hotel_id")]
		[Required]
		public int HotelId { get; set; }

		[Column("roomtype_name")]
		[Required]
		[MaxLength(200)]
		public string RoomTypeName { get; set; }

		[Column("roomtype_desc")]
		[MaxLength(500)]
		public string RoomTypeDesc { get; set; }

		[Column("roomtype_name_en")]
		[MaxLength(200)]
		public string? RoomTypeNameEn { get; set; }

		[Column("room_category")]
		[MaxLength(100)]
		public string? RoomCategory { get; set; }

		[Column("room_count")]
		public int RoomCount { get; set; }

		[Column("sort_order")]
		public int SortOrder { get; set; }

		[Column("is_active")]
		public bool IsActive { get; set; } = true;

		[Column("image_url")]
		[MaxLength(500)]
		public string? ImageUrl { get; set; }

		[Column("base_rate", TypeName = "decimal(12,2)")]
		public decimal? BaseRate { get; set; }

		[Column("season_rate", TypeName = "decimal(12,2)")]
		public decimal? SeasonRate { get; set; }

		/// <summary>
		/// Zaaer System ID (معرف Zaaer)
		/// External ID from Zaaer integration system
		/// </summary>
		[Column("zaaer_id")]
		public int? ZaaerId { get; set; }

		[Column("hall_gender_type")]
		[MaxLength(50)]
		public string? HallGenderType { get; set; }

		[Column("hall_capacity")]
		public int? HallCapacity { get; set; }

		[Column("allow_split")]
		public bool? AllowSplit { get; set; }

		[Column("minimum_booking_hours")]
		public int? MinimumBookingHours { get; set; }

		[Column("venue_kind")]
		[MaxLength(50)]
		public string? VenueKind { get; set; }

		// Navigation properties
		[ForeignKey("HotelId")]
		public HotelSettings HotelSettings { get; set; } = null!;
		public ICollection<Apartment> Apartments { get; set; } = new List<Apartment>();
	}
}

