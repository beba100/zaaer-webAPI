using zaaerIntegration.DTOs.Pms.Property;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IPmsPropertyService
    {
        Task<int> ResolveCurrentHotelIdAsync(CancellationToken cancellationToken = default);

        Task<PmsPropertyModeDto> GetPropertyModeAsync(CancellationToken cancellationToken = default);

        Task<PmsPropertyLookupsDto> GetLookupsAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PmsBuildingListItemDto>> ListBuildingsAsync(CancellationToken cancellationToken = default);
        Task<PmsBuildingDetailDto?> GetBuildingAsync(int id, CancellationToken cancellationToken = default);
        Task<PmsBuildingDetailDto> CreateBuildingAsync(PmsUpsertBuildingDto dto, CancellationToken cancellationToken = default);
        Task<PmsBuildingDetailDto?> UpdateBuildingAsync(int id, PmsUpsertBuildingDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteBuildingAsync(int id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PmsRoomTypeListItemDto>> ListRoomTypesAsync(CancellationToken cancellationToken = default);
        Task<PmsRoomTypeListItemDto?> GetRoomTypeAsync(int id, CancellationToken cancellationToken = default);
        Task<PmsRoomTypeListItemDto> CreateRoomTypeAsync(PmsUpsertRoomTypeDto dto, CancellationToken cancellationToken = default);
        Task<PmsRoomTypeListItemDto?> UpdateRoomTypeAsync(int id, PmsUpsertRoomTypeDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteRoomTypeAsync(int id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PmsApartmentListItemDto>> ListApartmentsAsync(
            string? search = null,
            int? buildingZaaerId = null,
            int? floorZaaerId = null,
            int? roomTypeZaaerId = null,
            int? parentApartmentZaaerId = null,
            CancellationToken cancellationToken = default);

        Task<PmsApartmentListItemDto?> GetApartmentAsync(int id, CancellationToken cancellationToken = default);
        Task<PmsApartmentListItemDto> CreateApartmentAsync(PmsUpsertApartmentDto dto, CancellationToken cancellationToken = default);
        Task<PmsApartmentListItemDto?> UpdateApartmentAsync(int id, PmsUpsertApartmentDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteApartmentAsync(int id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PmsFacilityListItemDto>> ListFacilitiesAsync(CancellationToken cancellationToken = default);
        Task<PmsFacilityListItemDto?> GetFacilityAsync(int id, CancellationToken cancellationToken = default);
        Task<PmsFacilityListItemDto> CreateFacilityAsync(PmsUpsertFacilityDto dto, CancellationToken cancellationToken = default);
        Task<PmsFacilityListItemDto?> UpdateFacilityAsync(int id, PmsUpsertFacilityDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteFacilityAsync(int id, CancellationToken cancellationToken = default);
    }
}
