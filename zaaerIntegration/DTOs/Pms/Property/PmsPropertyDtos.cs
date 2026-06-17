using System.ComponentModel.DataAnnotations;

namespace zaaerIntegration.DTOs.Pms.Property
{
    public sealed class PmsPropertyFloorLineDto
    {
        public int? FloorId { get; set; }
        public int? ZaaerId { get; set; }
        /// <summary>Parent block link (buildings.zaaer_id when integrated).</summary>
        public int? BuildingZaaerId { get; set; }
        public int FloorNumber { get; set; }
        public string? FloorName { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public sealed class PmsUpsertBuildingDto
    {
        public int? BuildingId { get; set; }
        public int? ZaaerId { get; set; }

        [Required]
        [MaxLength(200)]
        public string BuildingName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
        public List<PmsPropertyFloorLineDto> Floors { get; set; } = new();
    }

    public sealed class PmsBuildingListItemDto
    {
        public int BuildingId { get; set; }
        public int? ZaaerId { get; set; }
        public string BuildingName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int FloorCount { get; set; }
        public int ApartmentCount { get; set; }
    }

    public sealed class PmsBuildingDetailDto
    {
        public int BuildingId { get; set; }
        public int? ZaaerId { get; set; }
        public string BuildingName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int FloorCount { get; set; }
        public int ApartmentCount { get; set; }
        public List<PmsPropertyFloorLineDto> Floors { get; set; } = new();
    }

    public sealed class PmsUpsertRoomTypeDto
    {
        public int? RoomTypeId { get; set; }
        public int? ZaaerId { get; set; }

        [Required]
        [MaxLength(200)]
        public string RoomTypeName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? RoomTypeNameEn { get; set; }

        [MaxLength(500)]
        public string? RoomTypeDesc { get; set; }

        [MaxLength(100)]
        public string? RoomCategory { get; set; }

        public int RoomCount { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public string? ImageUrl { get; set; }
    }

    public sealed class PmsRoomTypeListItemDto
    {
        public int RoomTypeId { get; set; }
        public int? ZaaerId { get; set; }
        public string RoomTypeName { get; set; } = string.Empty;
        public string? RoomTypeNameEn { get; set; }
        public string? RoomTypeDesc { get; set; }
        public string? RoomCategory { get; set; }
        public int RoomCount { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public string? ImageUrl { get; set; }
        public int ApartmentCount { get; set; }
    }

    public sealed class PmsUpsertApartmentDto
    {
        public int? ApartmentId { get; set; }
        public int? ZaaerId { get; set; }

        [Required]
        [MaxLength(50)]
        public string ApartmentCode { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? ApartmentName { get; set; }

        public int? BuildingZaaerId { get; set; }
        public int? FloorZaaerId { get; set; }
        public int? RoomTypeZaaerId { get; set; }
        public int? ParentApartmentZaaerId { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "vacant";

        public bool IsActive { get; set; } = true;

        [MaxLength(50)]
        public string? TelephoneExtension { get; set; }
        public int BathroomsCount { get; set; }
        public string? KitchenType { get; set; }
        public string? HallType { get; set; }
        public string? ResortAreaType { get; set; }
        public int SingleBedsCount { get; set; }
        public int DoubleBedsCount { get; set; }
        public decimal? Area { get; set; }
        public string? Description { get; set; }
        public List<string>? Services { get; set; }
        public List<int>? FacilityZaaerIds { get; set; }
    }

    public sealed class PmsApartmentListItemDto
    {
        public int ApartmentId { get; set; }
        public int? ZaaerId { get; set; }
        public string ApartmentCode { get; set; } = string.Empty;
        public string? ApartmentName { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int? BuildingZaaerId { get; set; }
        public string? BuildingName { get; set; }
        public int? FloorZaaerId { get; set; }
        public string? FloorName { get; set; }
        public int? RoomTypeZaaerId { get; set; }
        public string? RoomTypeName { get; set; }
        public string? RoomCategory { get; set; }
        public int? ParentApartmentZaaerId { get; set; }
        public string? ParentApartmentName { get; set; }
        public int ChildUnitCount { get; set; }
        public string? KitchenType { get; set; }
        public string? HallType { get; set; }
        public string? ResortAreaType { get; set; }
        public int BathroomsCount { get; set; }
        public int SingleBedsCount { get; set; }
        public int DoubleBedsCount { get; set; }
        public decimal? Area { get; set; }
        public string? TelephoneExtension { get; set; }
        public string? Description { get; set; }
        public List<string> Services { get; set; } = new();
        public List<int> FacilityZaaerIds { get; set; } = new();
    }

    public sealed class PmsUpsertFacilityDto
    {
        public int? FacilityId { get; set; }
        public int? ZaaerId { get; set; }

        [Required]
        [MaxLength(200)]
        public string FacilityName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? FacilityNameEn { get; set; }

        public string? Description { get; set; }
        public int? BuildingZaaerId { get; set; }
        public int? FloorZaaerId { get; set; }
        public bool IsActive { get; set; } = true;
        public List<string>? ImageUrls { get; set; }
    }

    public sealed class PmsFacilityListItemDto
    {
        public int FacilityId { get; set; }
        public int? ZaaerId { get; set; }
        public string FacilityName { get; set; } = string.Empty;
        public string? FacilityNameEn { get; set; }
        public string? Description { get; set; }
        public int? BuildingZaaerId { get; set; }
        public string? BuildingName { get; set; }
        public int? FloorZaaerId { get; set; }
        public string? FloorName { get; set; }
        public bool IsActive { get; set; }
        public List<string> ImageUrls { get; set; } = new();
    }

    public sealed class PmsPropertyFacilityOptionDto
    {
        public int? ZaaerId { get; set; }
        public int FacilityId { get; set; }
        public string FacilityName { get; set; } = string.Empty;
        public string? FacilityNameEn { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public sealed class PmsPropertyModeLabelsDto
    {
        public string UnitLabelEn { get; set; } = "Room";
        public string UnitLabelAr { get; set; } = "غرفة";
        public string UnitTypeLabelEn { get; set; } = "Room Types";
        public string UnitTypeLabelAr { get; set; } = "أنواع الغرف";
        public string BoardTitleEn { get; set; } = "Room Board";
        public string BoardTitleAr { get; set; } = "لوحة الغرف";
    }

    public sealed class PmsPropertyModeDto
    {
        public string PropertyType { get; set; } = "hotel";
        public bool IsResort { get; set; }
        public bool IsHall { get; set; }
        public bool IsHotel { get; set; }
    }

    public sealed class PmsPropertyLookupsDto
    {
        public List<PmsBuildingListItemDto> Buildings { get; set; } = new();
        public List<PmsPropertyFloorLineDto> Floors { get; set; } = new();
        public List<PmsRoomTypeListItemDto> RoomTypes { get; set; } = new();
        public List<string> KitchenTypes { get; set; } = new();
        public List<string> HallTypes { get; set; } = new();
        public List<string> PropertyTypes { get; set; } = new();
        public string PropertyType { get; set; } = "hotel";
        public bool IsResort { get; set; }
        public bool IsHall { get; set; }
        public bool IsHotel { get; set; }
        public PmsPropertyModeLabelsDto Labels { get; set; } = new();
        public List<string> HallEventTypes { get; set; } = new();
        public List<string> HallEventStatuses { get; set; } = new();
        public List<string> HallGenderTypes { get; set; } = new();
        public List<string> HallVenueKinds { get; set; } = new();
        public List<string> HallPreparationStatuses { get; set; } = new();
        public List<string> PackagePriceTypes { get; set; } = new();
        public List<string> PackageCategories { get; set; } = new();
        public List<string> RoomCategories { get; set; } = new();
        public List<string> ResortAreaTypes { get; set; } = new();
        public List<string> ServiceOptions { get; set; } = new();
        public List<PmsPropertyFacilityOptionDto> Facilities { get; set; } = new();
    }
}
