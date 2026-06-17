#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.Pms.ReservationDetail
{
    public sealed class ReservationDetailDto
    {
        public int ReservationId { get; init; }
        public int? ZaaerId { get; init; }
        public int HotelId { get; init; }

        /// <summary>Tenant hotel code for <c>X-Hotel-Code</c> (from <c>hotel_settings.hotel_code</c>).</summary>
        public string? HotelCode { get; init; }
        public int? CustomerId { get; init; }
        /// <summary>Integration id stored on invoices / payment receipts (<c>customers.zaaer_id</c>).</summary>
        public int? CustomerZaaerId { get; init; }
        public int? CorporateId { get; init; }
        public ReservationDetailHeaderDto Header { get; init; } = new();
        public ReservationDetailGeneralDto General { get; init; } = new();
        public ReservationDetailDateDto Dates { get; init; } = new();
        public IReadOnlyList<ReservationDetailUnitDto> Units { get; init; } = Array.Empty<ReservationDetailUnitDto>();
        public ReservationDetailCorporateDto? Company { get; init; }
        public IReadOnlyList<ReservationDetailGuestDto> Guests { get; init; } = Array.Empty<ReservationDetailGuestDto>();

        /// <summary>Secondary guests — persisted in <c>dbo.reservation_companions</c>.</summary>
        public IReadOnlyList<ReservationDetailCompanionDto> Companions { get; init; } = Array.Empty<ReservationDetailCompanionDto>();

        /// <summary>Extra / add-on lines — persisted in <c>dbo.reservation_extras</c>.</summary>
        public IReadOnlyList<ReservationDetailExtraDto> Extras { get; init; } = Array.Empty<ReservationDetailExtraDto>();

        /// <summary>Discount lines — persisted in <c>dbo.discounts</c>.</summary>
        public IReadOnlyList<ReservationDetailDiscountDto> Discounts { get; init; } = Array.Empty<ReservationDetailDiscountDto>();

        /// <summary>Count of rows in <c>dbo.reservation_notes</c> for this reservation.</summary>
        public int NotesCount { get; init; }

        /// <summary>VAT / lodging (EWA) rates and <c>tax_included</c> flags from <c>taxes</c> for client-side extra line pricing.</summary>
        public ReservationDetailPricingTaxDto? PricingTax { get; init; }

        public ReservationDetailFinancialDto Financial { get; init; } = new();

        /// <summary>Pricing periods for read-only display; append requires <c>reservations.rental_periods</c>.</summary>
        public ReservationPeriodListResponseDto? Periods { get; init; }
    }

    public sealed class ReservationDetailPricingTaxDto
    {
        public decimal VatRate { get; init; }

        public decimal EwaRate { get; init; }

        public bool VatTaxIncluded { get; init; }

        public bool LodgingTaxIncluded { get; init; }
    }

    public sealed class ReservationDetailHeaderDto
    {
        public string ReservationNo { get; init; } = string.Empty;
        public string? Source { get; init; }
        public string? MainGuestName { get; init; }
        public DateTime? ActualArrival { get; init; }
        public string Status { get; init; } = string.Empty;
    }

    public sealed class ReservationDetailGeneralDto
    {
        public string ReservationType { get; init; } = string.Empty;
        public int? VisitPurposeId { get; init; }
        public string? VisitPurposeName { get; init; }
        public string? VisitPurposeNameAr { get; init; }
        public string? Source { get; init; }
        public string? CmBookingNo { get; init; }
    }

    public sealed class ReservationDetailDateDto
    {
        public string RentalType { get; init; } = string.Empty;
        public DateTime? CheckInDate { get; init; }
        public DateTime? CheckOutDate { get; init; }
        public DateTime? DepartureDate { get; init; }
        public int? NumberOfMonths { get; init; }
        public int? TotalNights { get; init; }
        /// <summary>ThirtyDay | Actual — monthly rental checkout calculation.</summary>
        public string? MonthlyCalendarMode { get; init; }
        public bool? IsAutoExtend { get; init; }
        public DateTime ReservationDate { get; init; }
    }

    public sealed class ReservationDetailUnitDto
    {
        public int UnitId { get; init; }
        public int? UnitZaaerId { get; init; }
        /// <summary>Internal apartments.apartment_id when the unit row resolves to an apartment.</summary>
        public int? ApartmentId { get; init; }
        /// <summary>Zaaer apartment id from apartments.zaaer_id (for reservation_units / integration).</summary>
        public int? ApartmentZaaerId { get; init; }
        /// <summary>Room/unit code for display (apartments.apartment_code).</summary>
        public string? ApartmentCode { get; init; }
        public string ApartmentLabel { get; init; } = string.Empty;
        public string? RoomTypeName { get; init; }
        public string? BuildingName { get; init; }
        public string? FloorName { get; init; }
        public DateTime CheckInDate { get; init; }
        public DateTime CheckOutDate { get; init; }
        public DateTime? DepartureDate { get; init; }
        public string UnitStatus { get; init; } = string.Empty;

        /// <summary>
        /// Tax-inclusive list gross from rate tables for this unit's apartment.
        /// </summary>
        public decimal? DefaultGrossRate { get; init; }

        /// <summary>
        /// daily_override | base_rates | room_type_fallback | programmatic_fallback | none
        /// </summary>
        public string? DefaultGrossRateSource { get; init; }

        /// <summary>Persisted net rent for the unit line (<c>reservation_units.rent_amount</c>).</summary>
        public decimal RentAmount { get; init; }

        /// <summary>Persisted tax-inclusive gross for the unit line (<c>reservation_units.total_amount</c>).</summary>
        public decimal TotalAmount { get; init; }
    }

    public sealed class ReservationDetailCorporateDto
    {
        public int CorporateId { get; init; }

        /// <summary>Integration id when allocated (<c>corporate_customers.zaaer_id</c>).</summary>
        public int? CorporateZaaerId { get; init; }

        /// <summary>Display number from central numbering (<c>cor_no</c>).</summary>
        public string? CorNo { get; init; }

        public string CorporateName { get; init; } = string.Empty;

        public string? CorporateNameAr { get; init; }

        public string? Country { get; init; }

        public string? CountryAr { get; init; }

        public string? City { get; init; }

        public string? CityAr { get; init; }

        public string? PostalCode { get; init; }

        public string? Address { get; init; }

        public string? AddressAr { get; init; }

        public string? VatRegistrationNo { get; init; }

        public string? CommercialRegistrationNo { get; init; }

        public string? DiscountMethod { get; init; }

        public decimal? DiscountValue { get; init; }

        public string? CorporatePhone { get; init; }
        public string? Email { get; init; }
        public string? ContactPersonName { get; init; }
        public string? ContactPersonPhone { get; init; }

        public string? Notes { get; init; }
    }

    public sealed class ReservationDetailGuestDto
    {
        public int CustomerId { get; init; }
        public int? CustomerZaaerId { get; init; }
        public bool IsPrimary { get; init; }
        public string CustomerName { get; init; } = string.Empty;
        public string? IdTypeName { get; init; }
        public string? IdTypeNameAr { get; init; }
        public string? IdNumber { get; init; }
        public DateTime? BirthDate { get; init; }
        public string? NationalityName { get; init; }
        public string? NationalityNameAr { get; init; }
        public string? MobileNo { get; init; }
        public string? Email { get; init; }
        public string? Gender { get; init; }
        public int? GtypeId { get; init; }
        public int? NationalityId { get; init; }
    }

    public sealed class ReservationDetailFinancialDto
    {
        public decimal? BalanceAmount { get; init; }
        public decimal? TotalAmount { get; init; }
        public decimal? AmountPaid { get; init; }
        public decimal? Subtotal { get; init; }
        public decimal? TotalTaxAmount { get; init; }

        /// <summary>Sum of extra / add-on line totals (from <c>reservation_extras</c>).</summary>
        public decimal? TotalExtra { get; init; }

        /// <summary>Total penalties applied to the reservation (<c>reservations.total_penalties</c>).</summary>
        public decimal? TotalPenalties { get; init; }

        /// <summary>Total discounts applied to the reservation (<c>reservations.total_discounts</c>).</summary>
        public decimal? TotalDiscounts { get; init; }
    }
}
