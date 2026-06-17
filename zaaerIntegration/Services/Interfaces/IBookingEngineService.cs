using zaaerIntegration.DTOs.BookingEngine;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IBookingEngineService
    {
        Task<IReadOnlyList<PublicHotelListItemDto>> GetPublicHotelsAsync(CancellationToken cancellationToken = default);

        Task<PublicHotelProfileDto?> GetHotelProfileAsync(string hotelCodeOrSlug, CancellationToken cancellationToken = default);

        Task<BookingSearchResponseDto?> SearchAsync(BookingSearchRequestDto request, CancellationToken cancellationToken = default);

        Task<BookingReturningGuestLookupDto> LookupReturningGuestAsync(
            string hotelCodeOrSlug,
            string? phone,
            CancellationToken cancellationToken = default);

        Task<BookingCouponValidateResponseDto?> ValidateCouponAsync(
            BookingCouponValidateRequestDto request,
            CancellationToken cancellationToken = default);

        Task<BookingConfirmResponseDto> ConfirmAsync(BookingConfirmRequestDto request, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<BookingEngineCouponDto>> ListCouponsAsync(int hotelId, CancellationToken cancellationToken = default);

        Task<BookingEngineCouponDto> CreateCouponAsync(
            int hotelId,
            BookingEngineCouponUpsertDto dto,
            CancellationToken cancellationToken = default);

        Task<BookingEngineCouponDto?> UpdateCouponAsync(
            int hotelId,
            int couponId,
            BookingEngineCouponUpsertDto dto,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteCouponAsync(int hotelId, int couponId, CancellationToken cancellationToken = default);

        Task<BookingEngineSettingsDto?> GetAdminSettingsAsync(int hotelId, CancellationToken cancellationToken = default);

        Task<BookingEngineSettingsDto> SaveAdminSettingsAsync(BookingEngineSettingsDto dto, CancellationToken cancellationToken = default);

        Task<BookingEngineMediaDto> AddMediaAsync(int hotelId, int? roomTypeId, string imageUrl, string? caption, bool isPrimary, CancellationToken cancellationToken = default);

        Task<bool> DeleteMediaAsync(int hotelId, int mediaId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<BookingEngineAvailabilityOverrideDto>> ListAvailabilityOverridesAsync(
            int hotelId,
            string? fromDate,
            string? toDate,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<BookingEngineAvailabilityOverrideDto>> SaveAvailabilityOverridesAsync(
            int hotelId,
            BookingEngineAvailabilityOverrideBatchDto batch,
            CancellationToken cancellationToken = default);
    }
}
