using FinanceLedgerAPI.Models;

namespace zaaerIntegration.Utilities
{
    /// <summary>
    /// Resolves property FK columns that store parent <c>zaaer_id</c> values.
    /// </summary>
    public static class PropertyEntityLinks
    {
        public static int GetBuildingLinkId(Building building)
        {
            if (building.ZaaerId.HasValue)
            {
                return building.ZaaerId.Value;
            }

            return building.BuildingId;
        }

        public static int? GetRoomTypeLinkId(RoomType? roomType) =>
            roomType == null ? null : roomType.ZaaerId ?? roomType.RoomTypeId;

        public static int? GetFloorLinkId(Floor? floor) =>
            floor == null ? null : floor.ZaaerId ?? floor.FloorId;

        public static bool FloorBelongsToBuilding(Floor floor, Building building) =>
            floor.BuildingId == building.BuildingId ||
            (building.ZaaerId.HasValue && floor.BuildingId == building.ZaaerId.Value);

        public static IEnumerable<Floor> FloorsForBuilding(IEnumerable<Floor> floors, Building building) =>
            floors.Where(f => FloorBelongsToBuilding(f, building));

        public static bool ApartmentReferencesFloor(Apartment apartment, Floor floor) =>
            apartment.FloorId == floor.FloorId ||
            (floor.ZaaerId.HasValue && apartment.FloorId == floor.ZaaerId.Value);

        public static bool ApartmentReferencesBuilding(Apartment apartment, Building building) =>
            ApartmentReferencesBuilding(apartment.BuildingId, building);

        public static bool ApartmentReferencesBuilding(int? apartmentBuildingId, Building building) =>
            apartmentBuildingId == building.BuildingId ||
            (building.ZaaerId.HasValue && apartmentBuildingId == building.ZaaerId.Value);
    }

    /// <summary>Lightweight apartment row for property lookups (avoids materializing legacy NULL int columns).</summary>
    public sealed record ApartmentScopeRow(
        int ApartmentId,
        int? ZaaerId,
        int? BuildingId,
        int? FloorId,
        int? RoomTypeId,
        int? ParentApartmentId,
        string ApartmentCode,
        string? ApartmentName);
}
