#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.BookingEngine
{
    public sealed class PublicHotelListItemDto
    {
        public string Code { get; init; } = string.Empty;
        public string? Name { get; init; }
        public string? NameEn { get; init; }
        public string? PublicSlug { get; init; }
        public string? City { get; init; }
        public string? CountryCode { get; init; }
        public string? LogoUrl { get; init; }
    }

    public sealed class PublicHotelProfileDto
    {
        public string Code { get; init; } = string.Empty;
        public int HotelId { get; init; }
        public string? Name { get; init; }
        public string? NameEn { get; init; }
        public string? City { get; init; }
        public string? CountryCode { get; init; }
        public string? Phone { get; init; }
        public string? Email { get; init; }
        public string? LogoUrl { get; init; }
        public string? FaviconUrl { get; init; }
        public string? BannerUrl { get; init; }
        public bool ShowHotelPicker { get; init; }
        public bool ShowCurrentBranchOnly { get; init; }
        public int MinimumStayNights { get; init; }
        public string? ButtonColor { get; init; }
        public string? BorderColor { get; init; }
        public string? BackgroundColor { get; init; }
        public string? TopFilterHtml { get; init; }
        public string? DownFilterHtml { get; init; }
        public string? ContactEmail { get; init; }
        public string? ContactPhone { get; init; }
        public string? ContactDescription { get; init; }
        public string DepositMode { get; init; } = "optional";
        public decimal? DepositAmount { get; init; }
        public decimal? DepositPercent { get; init; }
        public bool OnlineDepositEnabled { get; init; }
        public bool SalesClosed { get; init; }
        public string? SalesClosedMessage { get; init; }
        /// <summary>both | daily_only | monthly_only | hidden</summary>
        public string RentalTypeMode { get; init; } = "both";
        public PublicPromoBannerDto? PromoBanner { get; init; }
        /// <summary>True when the hotel has at least one active, currently redeemable promo coupon.</summary>
        public bool HasActiveCoupons { get; init; }
        public IReadOnlyList<PublicHotelListItemDto> Hotels { get; init; } = Array.Empty<PublicHotelListItemDto>();
    }

    public sealed class PublicPromoBannerDto
    {
        public string? ImageUrl { get; init; }
        public string? Html { get; init; }
        public DateTime? EndsAt { get; init; }
    }

    public sealed class BookingSearchRequestDto
    {
        public string? HotelCode { get; set; }
        public string? FromDate { get; set; }
        public string? ToDate { get; set; }
        public string? RentalType { get; set; }
        public int Adults { get; set; } = 1;
        public int Rooms { get; set; } = 1;
    }

    public sealed class BookingOfferFacilityDto
    {
        public string Label { get; init; } = string.Empty;
        public string? LabelEn { get; init; }
    }

    public sealed class BookingRoomOfferDto
    {
        public int RoomTypeId { get; init; }
        public int? RoomTypeZaaerId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? NameEn { get; init; }
        public string? Description { get; init; }
        public decimal? AreaSqm { get; init; }
        public decimal PricePerNight { get; init; }
        public decimal TotalPrice { get; init; }
        public decimal TaxAmount { get; init; }
        public decimal GrandTotal { get; init; }
        public int Nights { get; init; }
        public int AvailableUnits { get; init; }
        public IReadOnlyList<string> Images { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Highlights { get; init; } = Array.Empty<string>();
        public IReadOnlyList<BookingOfferFacilityDto> Facilities { get; init; } = Array.Empty<BookingOfferFacilityDto>();
        public IReadOnlyList<string> Services { get; init; } = Array.Empty<string>();
    }

    public sealed class BookingSearchResponseDto
    {
        public PublicHotelProfileDto Hotel { get; init; } = new();
        public IReadOnlyList<BookingRoomOfferDto> Offers { get; init; } = Array.Empty<BookingRoomOfferDto>();
    }

    public sealed class BookingConfirmLineDto
    {
        public int RoomTypeId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public sealed class BookingConfirmRequestDto
    {
        public string? HotelCode { get; set; }

        /// <summary>Legacy single room type (used when <see cref="Lines"/> is empty).</summary>
        public int RoomTypeId { get; set; }

        /// <summary>One or more room types with quantities in a single reservation.</summary>
        public IReadOnlyList<BookingConfirmLineDto>? Lines { get; set; }

        public string? FromDate { get; set; }
        public string? ToDate { get; set; }
        public string? RentalType { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public int? NationalityId { get; set; }
        public int? IdTypeId { get; set; }
        public string? IdNumber { get; set; }
        public string? Notes { get; set; }
        public bool PayDepositNow { get; set; }
        /// <summary>Guest-facing promo code (e.g. EID2026), not the internal CUP number.</summary>
        public string? CouponCode { get; set; }
    }

    public sealed class BookingCouponValidateRequestDto
    {
        public string? HotelCode { get; set; }
        public string? CouponCode { get; set; }
        public string? FromDate { get; set; }
        public string? ToDate { get; set; }
        public string? RentalType { get; set; }
        public IReadOnlyList<BookingConfirmLineDto>? Lines { get; set; }
    }

    public sealed class BookingCouponValidateResponseDto
    {
        public bool Valid { get; init; }
        public string? Message { get; init; }
        public string? PromoCode { get; init; }
        public string? Title { get; init; }
        public string? DiscountType { get; init; }
        public decimal DiscountValue { get; init; }
        public decimal DiscountAmount { get; init; }
        public decimal GrandTotalBefore { get; init; }
        public decimal GrandTotalAfter { get; init; }
    }

    public sealed class BookingEngineCouponDto
    {
        public int? CouponId { get; set; }
        public int HotelId { get; set; }
        public string CouponNo { get; set; } = string.Empty;
        public string PromoCode { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string DiscountType { get; set; } = "percent";
        public decimal DiscountValue { get; set; }
        public int? MinStayNights { get; set; }
        public decimal? MinBookingAmount { get; set; }
        public int? MaxRedemptions { get; set; }
        public int RedemptionCount { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public string? RoomTypeIds { get; set; }
        public bool IsActive { get; set; } = true;
        public string? Notes { get; set; }
    }

    public sealed class BookingEngineCouponUpsertDto
    {
        public string PromoCode { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string DiscountType { get; set; } = "percent";
        public decimal DiscountValue { get; set; }
        public int? MinStayNights { get; set; }
        public decimal? MinBookingAmount { get; set; }
        public int? MaxRedemptions { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public string? RoomTypeIds { get; set; }
        public bool IsActive { get; set; } = true;
        public string? Notes { get; set; }
    }

    /// <summary>Public lookup when guest enters a known mobile (returning guest pre-fill).</summary>
    public sealed class BookingReturningGuestLookupDto
    {
        public bool Found { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public string? Email { get; init; }
        public string? DisplayName { get; init; }
    }

    public sealed class BookingConfirmResponseDto
    {
        public bool Success { get; init; }
        public string? ReservationNo { get; init; }
        public int? ReservationId { get; init; }
        public string? Status { get; init; }
        public string? AssignedRoomCode { get; init; }
        public IReadOnlyList<string> AssignedRoomCodes { get; init; } = Array.Empty<string>();
        public decimal TotalAmount { get; init; }
        public decimal DiscountAmount { get; init; }
        public string? AppliedCouponCode { get; init; }
        public decimal DepositDue { get; init; }
        public decimal AmountPaid { get; init; }
        public string? PaymentStatus { get; init; }
        public string? Message { get; init; }
    }

    public sealed class BookingEngineSettingsDto
    {
        public int? SettingsId { get; set; }
        public int HotelId { get; set; }
        public bool IsEnabled { get; set; } = true;
        public string? PublicSlug { get; set; }
        public string? LogoUrl { get; set; }
        public string? FaviconUrl { get; set; }
        public string? BannerUrl { get; set; }
        public bool ShowHotelPicker { get; set; }
        public bool ShowCurrentBranchOnly { get; set; } = true;
        public int MinimumStayNights { get; set; } = 1;
        public string? ButtonColor { get; set; }
        public string? BorderColor { get; set; }
        public string? BackgroundColor { get; set; }
        public string? TopFilterHtml { get; set; }
        public string? DownFilterHtml { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactDescription { get; set; }
        public string DepositMode { get; set; } = "optional";
        public decimal? DepositAmount { get; set; }
        public decimal? DepositPercent { get; set; }
        public bool OnlineDepositEnabled { get; set; }
        public bool SalesClosed { get; set; }
        public string? SalesClosedMessage { get; set; }
        public string RentalTypeMode { get; set; } = "both";
        public bool PromoBannerEnabled { get; set; }
        public string? PromoBannerImageUrl { get; set; }
        public string? PromoBannerHtml { get; set; }
        public DateTime? PromoBannerEndsAt { get; set; }
        public string AvailabilityMode { get; set; } = "actual";
        public string RateFallbackMode { get; set; } = "standard";
        public decimal? RateFallbackMin { get; set; }
        public decimal? RateFallbackMax { get; set; }
        public string? HotelCode { get; set; }
        public IReadOnlyList<BookingEngineCouponDto> Coupons { get; set; } = Array.Empty<BookingEngineCouponDto>();
        public string? HotelName { get; set; }
        public string? PublicBookingUrl { get; set; }
        public IReadOnlyList<BookingEngineMediaDto> Media { get; set; } = Array.Empty<BookingEngineMediaDto>();
        public IReadOnlyList<BookingEngineAvailabilityOverrideDto> AvailabilityOverrides { get; set; } =
            Array.Empty<BookingEngineAvailabilityOverrideDto>();
    }

    public sealed class BookingEngineAvailabilityOverrideDto
    {
        public int? OverrideId { get; set; }
        public int RoomTypeId { get; set; }
        public string? RoomTypeName { get; set; }
        public string RateDate { get; set; } = string.Empty;
        public int DisplayUnits { get; set; }
    }

    public sealed class BookingEngineAvailabilityOverrideBatchDto
    {
        public List<BookingEngineAvailabilityOverrideUpsertDto> Items { get; set; } = new();
    }

    public sealed class BookingEngineAvailabilityOverrideUpsertDto
    {
        public int RoomTypeId { get; set; }
        public string DateFrom { get; set; } = string.Empty;
        public string DateTo { get; set; } = string.Empty;
        public int DisplayUnits { get; set; }
    }

    public sealed class BookingEngineMediaDto
    {
        public int? MediaId { get; set; }
        public int? RoomTypeId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? Caption { get; set; }
        public int SortOrder { get; set; }
        public bool IsPrimary { get; set; }
    }
}
