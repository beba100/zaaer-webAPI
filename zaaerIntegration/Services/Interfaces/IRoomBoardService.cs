#pragma warning disable CS1591

using zaaerIntegration.DTOs.RoomBoard;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IRoomBoardService
    {
        Task<RoomBoardResponseDto> GetRoomBoardAsync(
            RoomBoardRequestDto request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies a quick housekeeping / maintenance update for a single apartment (room board context menu).
        /// Returns (true, null) on success, or (false, errorCode) on validation / not found.
        /// </summary>
        Task<(bool Ok, string? ErrorCode)> ApplyApartmentQuickStateAsync(
            int apartmentRouteId,
            int? hotelId,
            string mode,
            CancellationToken cancellationToken = default);

        Task<(bool Found, IReadOnlyList<RoomBoardMaintenanceRowDto> Rows)> GetApartmentMaintenancesAsync(
            int apartmentRouteId,
            int? hotelId,
            CancellationToken cancellationToken = default);

        Task<(bool Ok, string? ErrorCode, int? MaintenanceId)> CreateApartmentMaintenanceAsync(
            int apartmentRouteId,
            int? hotelId,
            RoomBoardMaintenanceCreateRequestDto dto,
            int userId,
            CancellationToken cancellationToken = default);

        Task<(bool Ok, string? ErrorCode)> UpdateApartmentMaintenanceAsync(
            int apartmentRouteId,
            int? hotelId,
            int maintenanceId,
            RoomBoardMaintenanceUpdateRequestDto dto,
            CancellationToken cancellationToken = default);

        Task<(bool Ok, string? ErrorCode)> CancelApartmentMaintenanceAsync(
            int apartmentRouteId,
            int? hotelId,
            int maintenanceId,
            CancellationToken cancellationToken = default);
    }
}
