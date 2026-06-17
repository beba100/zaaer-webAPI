using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
	/// <summary>
	/// ���� �����/����� - Apartments/Rooms table
	/// </summary>
	[Table("apartments")]
	public class Apartment
	{
		[Key]
		[Column("apartment_id")]
		public int ApartmentId { get; set; }

		[Column("hotel_id")]
		[Required]
		public int HotelId { get; set; }

		[Column("building_id")]
		public int? BuildingId { get; set; }

		[Column("floor_id")]
		public int? FloorId { get; set; }

		[Column("roomtype_id")]
		public int? RoomTypeId { get; set; }

	/// <summary>Parent chalet/unit link for resort child rooms. Stores parent apartment zaaer_id when available.</summary>
	[Column("parent_apartment_id")]
	public int? ParentApartmentId { get; set; }

		[Column("apartment_code")]
		[Required]
		[MaxLength(50)]
		public string ApartmentCode { get; set; }

		[Column("apartment_name")]
		[MaxLength(200)]
		public string ApartmentName { get; set; }

	[Column("status")]
	[MaxLength(50)]
	public string Status { get; set; } = "vacant";

	/// <summary>
	/// Housekeeping Status (���� �������)
	/// Current housekeeping status of the apartment (e.g., "clean", "dirty", "inspected")
	/// </summary>
	[Column("housekeeping_status")]
	[MaxLength(50)]
	public string? HousekeepingStatus { get; set; }

	[Column("hall_preparation_status")]
	[MaxLength(50)]
	public string? HallPreparationStatus { get; set; }

	[Column("telephone_extension")]
	[MaxLength(50)]
	public string? TelephoneExtension { get; set; }

	[Column("bathrooms_count")]
	public int? BathroomsCount { get; set; }

	[Column("kitchen_type")]
	[MaxLength(50)]
	public string? KitchenType { get; set; }

	[Column("hall_type")]
	[MaxLength(50)]
	public string? HallType { get; set; }

	/// <summary>Resort chalet area: internal or external.</summary>
	[Column("resort_area_type")]
	[MaxLength(50)]
	public string? ResortAreaType { get; set; }

	[Column("single_beds_count")]
	public int? SingleBedsCount { get; set; }

	[Column("double_beds_count")]
	public int? DoubleBedsCount { get; set; }

	[Column("area", TypeName = "decimal(12,2)")]
	public decimal? Area { get; set; }

	[Column("description")]
	public string? Description { get; set; }

	[Column("is_active")]
	public bool? IsActive { get; set; } = true;

	[Column("services_json")]
	public string? ServicesJson { get; set; }

	/// <summary>Assigned facility zaaer_ids for this unit (JSON array of int).</summary>
	[Column("facilities_json")]
	public string? FacilitiesJson { get; set; }

	/// <summary>
	/// Zaaer System ID (���� Zaaer)
	/// External ID from Zaaer integration system
	/// </summary>
	[Column("zaaer_id")]
	public int? ZaaerId { get; set; }

        // Navigation properties
		[ForeignKey("HotelId")]
		public HotelSettings HotelSettings { get; set; } = null!;

        [ForeignKey("BuildingId")]
		public Building Building { get; set; } = null!;

		[ForeignKey("FloorId")]
		public Floor Floor { get; set; } = null!;

		[ForeignKey("RoomTypeId")]
		public RoomType RoomType { get; set; } = null!;

		public ICollection<ReservationUnit> ReservationUnits { get; set; } = new List<ReservationUnit>();
	}
}

