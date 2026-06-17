using zaaerIntegration.DTOs.Pms.Property;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IPmsRoomTypeRatesService
    {
        Task<int> ResolveCurrentHotelIdAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PmsRoomTypeRateListItemDto>> ListRoomTypeRatesAsync(CancellationToken cancellationToken = default);

        Task<PmsRoomTypeRateListItemDto?> UpdateRoomTypeRateAsync(
            int rateId,
            PmsUpdateRoomTypeRateDto dto,
            CancellationToken cancellationToken = default);

        Task<PmsRatesCalendarDto> GetRatesCalendarAsync(
            string? fromDate,
            string? toDate,
            CancellationToken cancellationToken = default);

        Task UpsertDailyRatesAsync(PmsUpsertDailyRatesDto dto, CancellationToken cancellationToken = default);
    }
}
