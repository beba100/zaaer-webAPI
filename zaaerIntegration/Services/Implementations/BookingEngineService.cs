#pragma warning disable CS1591

using System.Text.Json;
using FinanceLedgerAPI.Enums;
using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.BookingEngine;
using zaaerIntegration.Services.BookingEngine;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    public sealed partial class BookingEngineService : IBookingEngineService
    {
        private const string SourceBookingEngine = "booking_engine";

        private readonly BookingEngineDbFactory _dbFactory;
        private readonly MasterDbContext _masterDbContext;
        private readonly ITenantService _tenantService;
        private readonly INumberingService _numberingService;
        private readonly ILogger<BookingEngineService> _logger;

        public BookingEngineService(
            BookingEngineDbFactory dbFactory,
            MasterDbContext masterDbContext,
            ITenantService tenantService,
            INumberingService numberingService,
            ILogger<BookingEngineService> logger)
        {
            _dbFactory = dbFactory;
            _masterDbContext = masterDbContext;
            _tenantService = tenantService;
            _numberingService = numberingService;
            _logger = logger;
        }

        public async Task<IReadOnlyList<PublicHotelListItemDto>> GetPublicHotelsAsync(CancellationToken cancellationToken = default)
        {
            var tenants = await _masterDbContext.Tenants.AsNoTracking()
                .Where(t =>
                    t.Code != null && t.Code.Trim() != "" &&
                    t.DatabaseName != null && t.DatabaseName.Trim() != "")
                .OrderBy(t => t.Name)
                .ToListAsync(cancellationToken);

            var list = new List<PublicHotelListItemDto>();
            foreach (var tenant in tenants)
            {
                try
                {
                    await using var ctx = _dbFactory.CreateContext(tenant);
                    var hotel = await ctx.HotelSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
                    if (hotel == null)
                    {
                        continue;
                    }

                    var hotelScope = HotelScope.From(hotel);
                    var settings = await FindBookingEngineSettingsAsync(ctx, hotelScope, tracked: false, cancellationToken);

                    if (settings != null && !settings.IsEnabled)
                    {
                        continue;
                    }

                    list.Add(new PublicHotelListItemDto
                    {
                        Code = tenant.Code!.Trim(),
                        Name = hotel.HotelName ?? tenant.Name,
                        NameEn = hotel.HotelNameEn ?? tenant.NameEn,
                        PublicSlug = settings?.PublicSlug ?? hotel.HotelCode ?? tenant.Code,
                        City = hotel.City,
                        CountryCode = hotel.CountryCode,
                        LogoUrl = settings?.LogoUrl ?? hotel.LogoUrl
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Skipping tenant {Code} in public hotel list.", tenant.Code);
                }
            }

            return list;
        }

        public async Task<PublicHotelProfileDto?> GetHotelProfileAsync(string hotelCodeOrSlug, CancellationToken cancellationToken = default)
        {
            var (tenant, ctx, hotel) = await OpenHotelAsync(hotelCodeOrSlug, cancellationToken);
            if (tenant == null || ctx == null || hotel == null)
            {
                return null;
            }

            await using (ctx)
            {
                var hotelScope = HotelScope.From(hotel);
                var settings = await GetOrCreateSettingsTrackedAsync(ctx, hotelScope, cancellationToken);
                if (!settings.IsEnabled)
                {
                    return null;
                }

                var hotels = settings.ShowHotelPicker && !settings.ShowCurrentBranchOnly
                    ? await GetPublicHotelsAsync(cancellationToken)
                    : Array.Empty<PublicHotelListItemDto>();

                var hasActiveCoupons = await BookingEngineCouponHelper.HasAnyPublicCouponAsync(
                    ctx,
                    hotelScope.ScopeHotelId,
                    cancellationToken);

                return MapProfile(tenant, hotel, settings, hotels, hasActiveCoupons);
            }
        }

        public async Task<BookingSearchResponseDto?> SearchAsync(BookingSearchRequestDto request, CancellationToken cancellationToken = default)
        {
            var (tenant, ctx, hotel) = await OpenHotelAsync(request.HotelCode ?? string.Empty, cancellationToken);
            if (tenant == null || ctx == null || hotel == null)
            {
                return null;
            }

            await using (ctx)
            {
                var hotelScope = HotelScope.From(hotel);
                var settings = await FindBookingEngineSettingsAsync(ctx, hotelScope, tracked: false, cancellationToken);
                if (settings != null && !settings.IsEnabled)
                {
                    return null;
                }

                settings ??= await GetOrCreateSettingsTrackedAsync(ctx, hotelScope, cancellationToken);

                var hasActiveCoupons = await BookingEngineCouponHelper.HasAnyPublicCouponAsync(
                    ctx,
                    hotelScope.ScopeHotelId,
                    cancellationToken);

                if (settings.SalesClosed)
                {
                    var closedHotels = settings.ShowHotelPicker && !settings.ShowCurrentBranchOnly
                        ? await GetPublicHotelsAsync(cancellationToken)
                        : Array.Empty<PublicHotelListItemDto>();
                    return new BookingSearchResponseDto
                    {
                        Hotel = MapProfile(tenant, hotel, settings, closedHotels, hasActiveCoupons),
                        Offers = Array.Empty<BookingRoomOfferDto>()
                    };
                }

                if (!TryParseStayDates(request, settings, out var checkIn, out var checkOut, out var nights, out var error))
                {
                    throw new InvalidOperationException(error);
                }

                var rentalNorm = ResolveRentalForSearch(request.RentalType, settings);
                var taxConfig = await GetTaxConfigForHotelAsync(ctx, hotelScope, cancellationToken);

                var roomTypes = await ctx.RoomTypes.AsNoTracking()
                    .Where(r => r.HotelId == hotelScope.ScopeHotelId || r.HotelId == hotelScope.LocalHotelId)
                    .ToListAsync(cancellationToken);

                var apartmentTypeRefs = await ctx.Apartments.AsNoTracking()
                    .Where(a =>
                        a.RoomTypeId.HasValue &&
                        (a.HotelId == hotelScope.ScopeHotelId || a.HotelId == hotelScope.LocalHotelId))
                    .Select(a => a.RoomTypeId!.Value)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                var listedRefs = new HashSet<int>();
                foreach (var rt in roomTypes)
                {
                    listedRefs.Add(ResolveApartmentRoomTypeRef(rt));
                }

                foreach (var typeRef in apartmentTypeRefs)
                {
                    listedRefs.Add(typeRef);
                }

                var hotelApartments = await ctx.Apartments.AsNoTracking()
                    .Where(a =>
                        a.RoomTypeId.HasValue &&
                        (a.IsActive == null || a.IsActive == true) &&
                        (a.HotelId == hotelScope.ScopeHotelId || a.HotelId == hotelScope.LocalHotelId))
                    .ToListAsync(cancellationToken);

                var apartmentsByRoomType = hotelApartments
                    .GroupBy(a => a.RoomTypeId!.Value)
                    .ToDictionary(g => g.Key, g => (IReadOnlyList<Apartment>)g.ToList());

                var hotelFacilities = await ctx.Facilities.AsNoTracking()
                    .Where(f =>
                        f.IsActive &&
                        (f.HotelId == hotelScope.ScopeHotelId || f.HotelId == hotelScope.LocalHotelId))
                    .ToListAsync(cancellationToken);

                var facilityByZaaer = hotelFacilities
                    .Where(f => f.ZaaerId.HasValue)
                    .GroupBy(f => f.ZaaerId!.Value)
                    .ToDictionary(g => g.Key, g => g.First());

                var baseRates = await ctx.RoomTypeRates.AsNoTracking()
                    .Where(r => r.HotelId == hotelScope.ScopeHotelId || r.HotelId == hotelScope.LocalHotelId)
                    .ToListAsync(cancellationToken);

                var rateOptions = RoomTypeGrossRateOptions.FromBookingSettings(
                    settings?.RateFallbackMode,
                    settings?.RateFallbackMin,
                    settings?.RateFallbackMax);
                var availabilityMode = NormalizeAvailabilityMode(settings?.AvailabilityMode);
                var availOverrides = await ctx.BookingEngineAvailabilityOverrides.AsNoTracking()
                    .Where(o =>
                        (o.HotelId == hotelScope.ScopeHotelId || o.HotelId == hotelScope.LocalHotelId) &&
                        o.RateDate == checkIn.Date)
                    .ToListAsync(cancellationToken);

                var offers = new List<BookingRoomOfferDto>();
                foreach (var roomTypeRef in listedRefs.OrderBy(r => r))
                {
                    var rt = roomTypes.FirstOrDefault(r =>
                        r.ZaaerId == roomTypeRef || r.RoomTypeId == roomTypeRef);

                    var rateKeys = rt != null
                        ? RoomTypeRateQueryHelper.BuildRateKeys(rt)
                        : new HashSet<int> { roomTypeRef };
                    if (!rateKeys.Contains(roomTypeRef))
                    {
                        rateKeys.Add(roomTypeRef);
                    }

                    var actualAvailable = rt != null
                        ? await CountAvailableUnitsAsync(ctx, hotelScope, rt, checkIn, checkOut, cancellationToken)
                        : await CountAvailableUnitsByRefAsync(ctx, hotelScope, roomTypeRef, checkIn, checkOut, cancellationToken);

                    var overrideUnits = FindAvailabilityOverride(availOverrides, rateKeys);
                    var available = BookingEngineAvailabilityHelper.ResolveDisplayUnits(
                        actualAvailable,
                        overrideUnits,
                        availabilityMode);

                    var baseRate = baseRates.FirstOrDefault(r => RoomTypeRateResolver.RateMatchesRoomType(r, rateKeys));

                    var (grossNight, _) = await RoomTypeGrossRateResolver.ResolveAsync(
                        ctx,
                        hotelScope.ScopeHotelId,
                        hotelScope.LocalHotelId,
                        rateKeys,
                        baseRate,
                        rt,
                        rentalNorm,
                        checkIn.Date,
                        rateOptions,
                        cancellationToken);

                    if (grossNight <= 0m && available <= 0 && rt == null)
                    {
                        continue;
                    }

                    var scale = rentalNorm == "monthly" ? 1 : nights;
                    var perNightCalc = BookingEnginePricingHelper.Calculate(grossNight, taxConfig);
                    var totalCalc = BookingEnginePricingHelper.Calculate(grossNight * scale, taxConfig);
                    var images = rt != null
                        ? await LoadRoomImagesAsync(ctx, hotelScope, rt, cancellationToken)
                        : Array.Empty<string>();

                    var displayName = rt?.RoomTypeName ?? $"Room type {roomTypeRef}";
                    var displayNameEn = rt?.RoomTypeNameEn ?? displayName;

                    IReadOnlyList<Apartment> typeUnits = Array.Empty<Apartment>();
                    if (rt != null && apartmentsByRoomType.TryGetValue(rt.RoomTypeId, out var units))
                    {
                        typeUnits = units;
                    }

                    offers.Add(new BookingRoomOfferDto
                    {
                        RoomTypeId = rt?.ZaaerId ?? roomTypeRef,
                        RoomTypeZaaerId = rt?.ZaaerId ?? roomTypeRef,
                        Name = displayName,
                        NameEn = displayNameEn,
                        Description = rt?.RoomTypeDesc,
                        AreaSqm = typeUnits.Count > 0 ? typeUnits.Where(a => a.Area > 0).Select(a => a.Area).Max() : null,
                        PricePerNight = perNightCalc.Total,
                        TotalPrice = totalCalc.Net,
                        TaxAmount = totalCalc.Ewa + totalCalc.Vat,
                        GrandTotal = totalCalc.Total,
                        Nights = nights,
                        AvailableUnits = available,
                        Images = images,
                        Highlights = rt != null ? BuildHighlights(rt) : Array.Empty<string>(),
                        Facilities = rt != null
                            ? BuildOfferFacilities(rt, typeUnits, facilityByZaaer)
                            : Array.Empty<BookingOfferFacilityDto>(),
                        Services = BuildOfferServices(typeUnits)
                    });
                }

                offers = offers
                    .OrderBy(o => o.AvailableUnits <= 0 ? 1 : 0)
                    .ThenBy(o => o.Name)
                    .ToList();

                settings ??= await GetOrCreateSettingsTrackedAsync(ctx, hotelScope, cancellationToken);
                var hotels = settings.ShowHotelPicker && !settings.ShowCurrentBranchOnly
                    ? await GetPublicHotelsAsync(cancellationToken)
                    : Array.Empty<PublicHotelListItemDto>();

                return new BookingSearchResponseDto
                {
                    Hotel = MapProfile(tenant, hotel, settings, hotels, hasActiveCoupons),
                    Offers = offers
                };
            }
        }

        public async Task<BookingReturningGuestLookupDto> LookupReturningGuestAsync(
            string hotelCodeOrSlug,
            string? phone,
            CancellationToken cancellationToken = default)
        {
            var normalized = PhoneNumberNormalizer.NormalizeMobileForStorage(phone);
            if (string.IsNullOrWhiteSpace(normalized) || normalized.Length < 11)
            {
                return new BookingReturningGuestLookupDto { Found = false };
            }

            var (tenant, ctx, hotel) = await OpenHotelAsync(hotelCodeOrSlug, cancellationToken);
            if (tenant == null || ctx == null || hotel == null)
            {
                return new BookingReturningGuestLookupDto { Found = false };
            }

            await using (ctx)
            {
                var hotelScope = HotelScope.From(hotel);
                var customer = await FindCustomerByNormalizedPhoneAsync(
                    ctx,
                    hotelScope.ScopeHotelId,
                    normalized,
                    cancellationToken);

                if (customer == null)
                {
                    return new BookingReturningGuestLookupDto { Found = false };
                }

                SplitCustomerName(customer.CustomerName, out var first, out var last);
                return new BookingReturningGuestLookupDto
                {
                    Found = true,
                    FirstName = first,
                    LastName = last,
                    Email = string.IsNullOrWhiteSpace(customer.Email) ? null : customer.Email.Trim(),
                    DisplayName = customer.CustomerName
                };
            }
        }

        public async Task<BookingConfirmResponseDto> ConfirmAsync(BookingConfirmRequestDto request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            {
                return Fail("First and last name are required.");
            }

            var (tenant, ctx, hotel) = await OpenHotelAsync(request.HotelCode ?? string.Empty, cancellationToken);
            if (tenant == null || ctx == null || hotel == null)
            {
                return Fail("Hotel not found.");
            }

            await using (ctx)
            {
                _tenantService.SetCurrentTenant(tenant);

                var hotelScope = HotelScope.From(hotel);
                var settings = await FindBookingEngineSettingsAsync(ctx, hotelScope, tracked: false, cancellationToken);

                if (settings != null && !settings.IsEnabled)
                {
                    return Fail("Booking is not available for this property.");
                }

                settings ??= await GetOrCreateSettingsTrackedAsync(ctx, hotelScope, cancellationToken);

                if (settings.SalesClosed)
                {
                    var msg = string.IsNullOrWhiteSpace(settings.SalesClosedMessage)
                        ? "Online booking is temporarily closed."
                        : settings.SalesClosedMessage.Trim();
                    return Fail(msg);
                }

                if (!TryParseStayDates(
                        new BookingSearchRequestDto
                        {
                            FromDate = request.FromDate,
                            ToDate = request.ToDate,
                            RentalType = request.RentalType
                        },
                        settings,
                        out var checkIn,
                        out var checkOut,
                        out var nights,
                        out var dateError))
                {
                    return Fail(dateError);
                }

                checkIn = KsaTime.CombineDateWithCurrentTime(checkIn);
                checkOut = KsaTime.DefaultDepartureAtSixPm(checkOut);

                var lines = NormalizeConfirmLines(request);
                if (lines.Count == 0)
                {
                    return Fail("Select at least one room.");
                }

                var rentalNorm = ResolveRentalForSearch(request.RentalType, settings);
                var taxConfig = await GetTaxConfigForHotelAsync(ctx, hotelScope, cancellationToken);
                var scale = rentalNorm == "monthly" ? 1 : nights;

                var linePlans = new List<ConfirmLinePlan>();
                var excludeApartmentIds = new HashSet<int>();

                foreach (var line in lines)
                {
                    var roomType = await ResolveRoomTypeAsync(ctx, hotelScope, line.RoomTypeId, cancellationToken);
                    if (roomType == null)
                    {
                        return Fail($"Room type not found (id {line.RoomTypeId}).");
                    }

                    var apartments = await PickAvailableApartmentsAsync(
                        ctx,
                        hotelScope,
                        roomType,
                        checkIn,
                        checkOut,
                        line.Quantity,
                        excludeApartmentIds,
                        cancellationToken);

                    if (apartments.Count < line.Quantity)
                    {
                        return Fail($"Not enough rooms available for {roomType.RoomTypeName ?? roomType.RoomTypeNameEn ?? "selected type"}.");
                    }

                    var roomTypeRateRef = ResolveApartmentRoomTypeRef(roomType);
                    var rateKeys = RoomTypeRateQueryHelper.BuildRateKeys(roomType);
                    var baseRate = await ctx.RoomTypeRates.AsNoTracking()
                        .Where(r => r.HotelId == hotelScope.ScopeHotelId || r.HotelId == hotelScope.LocalHotelId)
                        .ToListAsync(cancellationToken);
                    var matchedRate = baseRate.FirstOrDefault(r => RoomTypeRateResolver.RateMatchesRoomType(r, rateKeys));
                    var rateOptions = RoomTypeGrossRateOptions.FromBookingSettings(
                        settings.RateFallbackMode,
                        settings.RateFallbackMin,
                        settings.RateFallbackMax);
                    var (grossNight, _) = await RoomTypeGrossRateResolver.ResolveAsync(
                        ctx,
                        hotelScope.ScopeHotelId,
                        hotelScope.LocalHotelId,
                        rateKeys,
                        matchedRate,
                        roomType,
                        rentalNorm,
                        checkIn.Date,
                        rateOptions,
                        cancellationToken);
                    if (grossNight <= 0m)
                    {
                        return Fail($"Pricing is not configured for {roomType.RoomTypeName ?? "room type"}.");
                    }

                    var calc = BookingEnginePricingHelper.Calculate(grossNight * scale, taxConfig);
                    linePlans.Add(new ConfirmLinePlan(roomType, apartments, calc, grossNight));
                }

                var totalNet = linePlans.Sum(p => p.Pricing.Net * p.Apartments.Count);
                var totalVat = linePlans.Sum(p => p.Pricing.Vat * p.Apartments.Count);
                var totalEwa = linePlans.Sum(p => p.Pricing.Ewa * p.Apartments.Count);
                var totalTax = Math.Round(totalVat + totalEwa, 2, MidpointRounding.AwayFromZero);
                var grandTotal = linePlans.Sum(p => p.Pricing.Total * p.Apartments.Count);

                var (appliedCoupon, discountAmount, couponError) = await ResolveCouponForConfirmAsync(
                    ctx,
                    hotelScope,
                    request,
                    grandTotal,
                    nights,
                    checkIn,
                    cancellationToken);
                if (couponError != null)
                {
                    return Fail(couponError);
                }

                var finalTotal = Math.Round(grandTotal - discountAmount, 2, MidpointRounding.AwayFromZero);

                var depositDue = CalculateDepositDue(settings, finalTotal, request.PayDepositNow);
                const string headerStatus = "unconfirmed";
                const string unitStatus = "unconfirmed";
                const decimal amountPaid = 0m;

                var customerName = $"{request.FirstName.Trim()} {request.LastName.Trim()}".Trim();
                long? customerAuditId = null;
                long? reservationAuditId = null;
                Reservation? created = null;
                var assignedCodes = new List<string>();

                await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var (customer, newCustomerAuditId) = await ResolveOrCreateBookingCustomerAsync(
                        ctx,
                        hotelScope,
                        request,
                        customerName,
                        cancellationToken);
                    customerAuditId = newCustomerAuditId;
                    var customerStoredId = customer.ZaaerId is > 0 ? customer.ZaaerId.Value : customer.CustomerId;

                    var resIdentity = await _numberingService.GetNextBusinessIdentityAsync(
                        "reservation",
                        hotelScope.ScopeHotelId,
                        null,
                        $"booking-reservation:{hotelScope.ScopeHotelId}:{Guid.NewGuid():N}",
                        cancellationToken);
                    reservationAuditId = resIdentity.AuditId;

                    var resZaaerId = ZaaerIdMapper.ToNullableInt32(resIdentity.ZaaerId);
                    var reservationLinkId = resZaaerId ?? 0;

                    created = new Reservation
                    {
                        HotelId = hotelScope.ScopeHotelId,
                        CustomerId = customerStoredId,
                        ReservationNo = resIdentity.DocumentNo,
                        ZaaerId = resZaaerId,
                        ExternalRefNo = resZaaerId,
                        ReservationType = "individual",
                        RentalType = rentalNorm,
                        Status = headerStatus,
                        Source = SourceBookingEngine,
                        ReservationDate = KsaTime.Now,
                        CheckInDate = checkIn,
                        CheckOutDate = checkOut,
                        DepartureDate = checkOut,
                        TotalNights = nights,
                        Subtotal = totalNet,
                        VatRate = taxConfig.VatRate,
                        VatAmount = totalVat,
                        LodgingTaxRate = taxConfig.EwaRate,
                        LodgingTaxAmount = totalEwa,
                        TotalTaxAmount = totalTax,
                        TotalAmount = finalTotal,
                        TotalDiscounts = discountAmount > 0m ? discountAmount : null,
                        AmountPaid = amountPaid,
                        BalanceAmount = Math.Round(finalTotal - amountPaid, 2, MidpointRounding.AwayFromZero),
                        BookingCouponId = appliedCoupon?.CouponId,
                        CouponPromoCode = appliedCoupon?.PromoCode,
                        CreatedAt = KsaTime.Now
                    };

                    ctx.Reservations.Add(created);
                    await ctx.SaveChangesAsync(cancellationToken);

                    if (appliedCoupon != null)
                    {
                        appliedCoupon.RedemptionCount += 1;
                        appliedCoupon.UpdatedAt = KsaTime.Now;
                    }

                    if (reservationLinkId <= 0)
                    {
                        reservationLinkId = created.ReservationId;
                    }

                    var unitDayRateSeeds = new List<(ReservationUnit Unit, decimal GrossNight)>();

                    foreach (var plan in linePlans)
                    {
                        foreach (var apartment in plan.Apartments)
                        {
                            var apartmentStoredId = apartment.ZaaerId is > 0 ? apartment.ZaaerId.Value : apartment.ApartmentId;
                            var unit = new ReservationUnit
                            {
                                ReservationId = reservationLinkId,
                                ApartmentId = apartmentStoredId,
                                CheckInDate = checkIn,
                                CheckOutDate = checkOut,
                                DepartureDate = checkOut,
                                NumberOfNights = nights,
                                RentAmount = plan.Pricing.Net,
                                VatRate = taxConfig.VatRate,
                                VatAmount = plan.Pricing.Vat,
                                LodgingTaxRate = taxConfig.EwaRate,
                                LodgingTaxAmount = plan.Pricing.Ewa,
                                TotalAmount = plan.Pricing.Total,
                                Status = unitStatus,
                                CreatedAt = KsaTime.Now
                            };

                            ctx.ReservationUnits.Add(unit);
                            unitDayRateSeeds.Add((unit, plan.GrossNight));
                            apartment.Status = "reserved";
                            if (!string.IsNullOrWhiteSpace(apartment.ApartmentCode))
                            {
                                assignedCodes.Add(apartment.ApartmentCode!);
                            }
                        }
                    }

                    await ctx.SaveChangesAsync(cancellationToken);

                    var rateReservationId = created.ZaaerId ?? created.ReservationId;
                    SeedReservationUnitDayRates(
                        ctx,
                        rateReservationId,
                        unitDayRateSeeds,
                        checkIn,
                        checkOut,
                        rentalNorm,
                        taxConfig);

                    await ctx.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    if (customerAuditId.HasValue)
                    {
                        await _numberingService.MarkVoidedAsync(customerAuditId.Value, ex.Message, cancellationToken);
                    }

                    if (reservationAuditId.HasValue)
                    {
                        await _numberingService.MarkVoidedAsync(reservationAuditId.Value, ex.Message, cancellationToken);
                    }

                    _logger.LogError(ex, "Booking confirm failed for hotel {HotelId}", hotel.HotelId);
                    return Fail("Could not complete booking. Please try again.");
                }

                if (customerAuditId.HasValue)
                {
                    await _numberingService.MarkCommittedAsync(customerAuditId.Value, cancellationToken);
                }

                if (reservationAuditId.HasValue)
                {
                    await _numberingService.MarkCommittedAsync(reservationAuditId.Value, cancellationToken);
                }

                return new BookingConfirmResponseDto
                {
                    Success = true,
                    ReservationNo = created!.ReservationNo,
                    ReservationId = created.ZaaerId ?? created.ReservationId,
                    Status = created.Status,
                    AssignedRoomCode = assignedCodes.FirstOrDefault(),
                    AssignedRoomCodes = assignedCodes,
                    TotalAmount = created.TotalAmount ?? finalTotal,
                    DiscountAmount = discountAmount,
                    AppliedCouponCode = appliedCoupon?.PromoCode,
                    DepositDue = depositDue,
                    AmountPaid = amountPaid,
                    PaymentStatus = depositDue > 0m ? "deposit_pending" : "none",
                    Message = "Booking received. Reservation is unconfirmed until the hotel reviews it."
                };
            }
        }

        public async Task<BookingEngineSettingsDto?> GetAdminSettingsAsync(int hotelId, CancellationToken cancellationToken = default)
        {
            await using var ctx = OpenAdminContext();
            var hotel = await ctx.HotelSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            if (hotel == null)
            {
                return null;
            }

            var hotelScope = HotelScope.From(hotel);
            var settings = await FindBookingEngineSettingsAsync(ctx, hotelScope, tracked: false, cancellationToken);

            var media = await ctx.BookingEngineMedia.AsNoTracking()
                .Where(m => m.HotelId == hotelScope.ScopeHotelId || m.HotelId == hotelScope.LocalHotelId)
                .OrderBy(m => m.SortOrder)
                .ThenBy(m => m.MediaId)
                .Select(m => new BookingEngineMediaDto
                {
                    MediaId = m.MediaId,
                    RoomTypeId = m.RoomTypeId,
                    ImageUrl = m.ImageUrl,
                    Caption = m.Caption,
                    SortOrder = m.SortOrder,
                    IsPrimary = m.IsPrimary
                })
                .ToListAsync(cancellationToken);

            var tenantCode = _tenantService.GetTenant()?.Code?.Trim();
            var publicHotelKey = tenantCode ?? hotel.HotelCode ?? settings?.PublicSlug ?? "";
            var publicUrl = BuildPublicUrl(publicHotelKey);

            return new BookingEngineSettingsDto
            {
                SettingsId = settings?.SettingsId,
                HotelId = hotelScope.ScopeHotelId,
                IsEnabled = settings?.IsEnabled ?? true,
                PublicSlug = settings?.PublicSlug,
                LogoUrl = settings?.LogoUrl ?? hotel.LogoUrl,
                FaviconUrl = settings?.FaviconUrl,
                BannerUrl = settings?.BannerUrl,
                ShowHotelPicker = settings?.ShowHotelPicker ?? false,
                ShowCurrentBranchOnly = settings?.ShowCurrentBranchOnly ?? true,
                MinimumStayNights = settings?.MinimumStayNights ?? 1,
                ButtonColor = settings?.ButtonColor,
                BorderColor = settings?.BorderColor,
                BackgroundColor = settings?.BackgroundColor,
                TopFilterHtml = PmsHtmlSanitizer.SanitizeRichText(settings?.TopFilterHtml),
                DownFilterHtml = PmsHtmlSanitizer.SanitizeRichText(settings?.DownFilterHtml),
                ContactEmail = settings?.ContactEmail ?? hotel.Email,
                ContactPhone = settings?.ContactPhone ?? hotel.Phone,
                ContactDescription = settings?.ContactDescription,
                DepositMode = settings?.DepositMode ?? "optional",
                DepositAmount = settings?.DepositAmount,
                DepositPercent = settings?.DepositPercent,
                OnlineDepositEnabled = settings?.OnlineDepositEnabled ?? false,
                SalesClosed = settings?.SalesClosed ?? false,
                SalesClosedMessage = settings?.SalesClosedMessage,
                RentalTypeMode = NormalizeRentalTypeMode(settings?.RentalTypeMode),
                PromoBannerEnabled = settings?.PromoBannerEnabled ?? false,
                PromoBannerImageUrl = settings?.PromoBannerImageUrl,
                PromoBannerHtml = PmsHtmlSanitizer.SanitizeRichText(settings?.PromoBannerHtml),
                PromoBannerEndsAt = settings?.PromoBannerEndsAt,
                AvailabilityMode = NormalizeAvailabilityMode(settings?.AvailabilityMode),
                RateFallbackMode = NormalizeRateFallbackMode(settings?.RateFallbackMode),
                RateFallbackMin = settings?.RateFallbackMin,
                RateFallbackMax = settings?.RateFallbackMax,
                HotelCode = hotel.HotelCode,
                HotelName = hotel.HotelName,
                PublicBookingUrl = publicUrl,
                Media = media,
                Coupons = await ListCouponsAsync(hotelScope.ScopeHotelId, cancellationToken),
                AvailabilityOverrides = await ListAvailabilityOverridesAsync(
                    hotelScope.ScopeHotelId,
                    KsaTime.Now.ToString("yyyy-MM-dd"),
                    KsaTime.Now.AddDays(30).ToString("yyyy-MM-dd"),
                    cancellationToken)
            };
        }

        public async Task<BookingEngineSettingsDto> SaveAdminSettingsAsync(BookingEngineSettingsDto dto, CancellationToken cancellationToken = default)
        {
            await using var ctx = OpenAdminContext();
            var hotel = await ctx.HotelSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Hotel settings not found.");
            var hotelScope = HotelScope.From(hotel);

            var entity = await FindBookingEngineSettingsAsync(ctx, hotelScope, tracked: true, cancellationToken);

            if (entity == null)
            {
                entity = new BookingEngineSettings { HotelId = hotelScope.ScopeHotelId, CreatedAt = KsaTime.Now };
                ctx.BookingEngineSettings.Add(entity);
            }
            else if (entity.HotelId == hotelScope.LocalHotelId && hotelScope.LocalHotelId != hotelScope.ScopeHotelId)
            {
                entity.HotelId = hotelScope.ScopeHotelId;
            }

            entity.IsEnabled = dto.IsEnabled;
            entity.PublicSlug = string.IsNullOrWhiteSpace(dto.PublicSlug) ? null : dto.PublicSlug.Trim();
            entity.LogoUrl = dto.LogoUrl;
            entity.FaviconUrl = dto.FaviconUrl;
            entity.BannerUrl = dto.BannerUrl;
            entity.ShowHotelPicker = dto.ShowHotelPicker;
            entity.ShowCurrentBranchOnly = dto.ShowCurrentBranchOnly;
            entity.MinimumStayNights = Math.Max(1, dto.MinimumStayNights);
            entity.ButtonColor = dto.ButtonColor;
            entity.BorderColor = dto.BorderColor;
            entity.BackgroundColor = dto.BackgroundColor;
            entity.TopFilterHtml = PmsHtmlSanitizer.SanitizeRichText(dto.TopFilterHtml);
            entity.DownFilterHtml = PmsHtmlSanitizer.SanitizeRichText(dto.DownFilterHtml);
            entity.ContactEmail = dto.ContactEmail;
            entity.ContactPhone = dto.ContactPhone;
            entity.ContactDescription = dto.ContactDescription;
            entity.DepositMode = string.IsNullOrWhiteSpace(dto.DepositMode) ? "optional" : dto.DepositMode.Trim().ToLowerInvariant();
            entity.DepositAmount = dto.DepositAmount;
            entity.DepositPercent = dto.DepositPercent;
            entity.OnlineDepositEnabled = dto.OnlineDepositEnabled;
            entity.SalesClosed = dto.SalesClosed;
            entity.SalesClosedMessage = string.IsNullOrWhiteSpace(dto.SalesClosedMessage)
                ? null
                : dto.SalesClosedMessage.Trim();
            entity.RentalTypeMode = NormalizeRentalTypeMode(dto.RentalTypeMode);
            entity.PromoBannerEnabled = dto.PromoBannerEnabled;
            entity.PromoBannerImageUrl = dto.PromoBannerImageUrl;
            entity.PromoBannerHtml = PmsHtmlSanitizer.SanitizeRichText(dto.PromoBannerHtml);
            entity.PromoBannerEndsAt = dto.PromoBannerEndsAt;
            entity.AvailabilityMode = NormalizeAvailabilityMode(dto.AvailabilityMode);
            entity.RateFallbackMode = NormalizeRateFallbackMode(dto.RateFallbackMode);
            entity.RateFallbackMin = dto.RateFallbackMin;
            entity.RateFallbackMax = dto.RateFallbackMax;
            entity.UpdatedAt = KsaTime.Now;

            await ctx.SaveChangesAsync(cancellationToken);
            return (await GetAdminSettingsAsync(hotelScope.ScopeHotelId, cancellationToken))!;
        }

        public async Task<BookingEngineMediaDto> AddMediaAsync(
            int hotelId,
            int? roomTypeId,
            string imageUrl,
            string? caption,
            bool isPrimary,
            CancellationToken cancellationToken = default)
        {
            await using var ctx = OpenAdminContext();
            var hotel = await ctx.HotelSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Hotel settings not found.");
            var hotelScope = HotelScope.From(hotel);
            var storeHotelId = hotelScope.ScopeHotelId;

            if (isPrimary)
            {
                var existing = await ctx.BookingEngineMedia
                    .Where(m =>
                        (m.HotelId == hotelScope.ScopeHotelId || m.HotelId == hotelScope.LocalHotelId) &&
                        m.RoomTypeId == roomTypeId)
                    .ToListAsync(cancellationToken);
                foreach (var row in existing)
                {
                    row.IsPrimary = false;
                }
            }

            var maxSort = await ctx.BookingEngineMedia
                .Where(m =>
                    (m.HotelId == hotelScope.ScopeHotelId || m.HotelId == hotelScope.LocalHotelId) &&
                    m.RoomTypeId == roomTypeId)
                .Select(m => (int?)m.SortOrder)
                .MaxAsync(cancellationToken) ?? 0;

            var media = new BookingEngineMedia
            {
                HotelId = storeHotelId,
                RoomTypeId = roomTypeId,
                ImageUrl = imageUrl,
                Caption = caption,
                SortOrder = maxSort + 1,
                IsPrimary = isPrimary,
                CreatedAt = KsaTime.Now
            };

            ctx.BookingEngineMedia.Add(media);
            await ctx.SaveChangesAsync(cancellationToken);

            return new BookingEngineMediaDto
            {
                MediaId = media.MediaId,
                RoomTypeId = media.RoomTypeId,
                ImageUrl = media.ImageUrl,
                Caption = media.Caption,
                SortOrder = media.SortOrder,
                IsPrimary = media.IsPrimary
            };
        }

        public async Task<bool> DeleteMediaAsync(int hotelId, int mediaId, CancellationToken cancellationToken = default)
        {
            await using var ctx = OpenAdminContext();
            var hotel = await ctx.HotelSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            if (hotel == null)
            {
                return false;
            }

            var hotelScope = HotelScope.From(hotel);
            var row = await ctx.BookingEngineMedia
                .FirstOrDefaultAsync(
                    m => m.MediaId == mediaId &&
                         (m.HotelId == hotelScope.ScopeHotelId || m.HotelId == hotelScope.LocalHotelId),
                    cancellationToken);
            if (row == null)
            {
                return false;
            }

            ctx.BookingEngineMedia.Remove(row);
            await ctx.SaveChangesAsync(cancellationToken);
            return true;
        }

        private ApplicationDbContext OpenAdminContext()
        {
            var tenant = _tenantService.GetTenant();
            if (tenant == null)
            {
                throw new InvalidOperationException("Tenant not resolved. Provide a valid X-Hotel-Code header.");
            }

            return _dbFactory.CreateContext(tenant);
        }

        private async Task<(Tenant? Tenant, ApplicationDbContext? Ctx, HotelSettings? Hotel)> OpenHotelAsync(
            string hotelCodeOrSlug,
            CancellationToken cancellationToken)
        {
            var key = (hotelCodeOrSlug ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                return (null, null, null);
            }

            var tenant = await _dbFactory.ResolveTenantByCodeAsync(key, cancellationToken)
                         ?? await _dbFactory.ResolveTenantBySlugAsync(key, cancellationToken);
            if (tenant == null)
            {
                return (null, null, null);
            }

            var ctx = _dbFactory.CreateContext(tenant);
            var hotel = await ctx.HotelSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            return (tenant, ctx, hotel);
        }

        private static PublicHotelProfileDto MapProfile(
            Tenant tenant,
            HotelSettings hotel,
            BookingEngineSettings settings,
            IReadOnlyList<PublicHotelListItemDto> hotels,
            bool hasActiveCoupons = false)
        {
            return new PublicHotelProfileDto
            {
                Code = tenant.Code?.Trim() ?? string.Empty,
                HotelId = hotel.HotelId,
                Name = hotel.HotelName,
                NameEn = hotel.HotelNameEn,
                City = hotel.City,
                CountryCode = hotel.CountryCode,
                Phone = settings.ContactPhone ?? hotel.Phone,
                Email = settings.ContactEmail ?? hotel.Email,
                LogoUrl = settings.LogoUrl ?? hotel.LogoUrl,
                FaviconUrl = settings.FaviconUrl,
                BannerUrl = settings.BannerUrl,
                ShowHotelPicker = settings.ShowHotelPicker && !settings.ShowCurrentBranchOnly,
                ShowCurrentBranchOnly = settings.ShowCurrentBranchOnly,
                MinimumStayNights = settings.MinimumStayNights,
                ButtonColor = settings.ButtonColor,
                BorderColor = settings.BorderColor,
                BackgroundColor = settings.BackgroundColor,
                TopFilterHtml = PmsHtmlSanitizer.SanitizeRichText(settings.TopFilterHtml),
                DownFilterHtml = PmsHtmlSanitizer.SanitizeRichText(settings.DownFilterHtml),
                ContactEmail = settings.ContactEmail,
                ContactPhone = settings.ContactPhone,
                ContactDescription = settings.ContactDescription,
                DepositMode = settings.DepositMode,
                DepositAmount = settings.DepositAmount,
                DepositPercent = settings.DepositPercent,
                OnlineDepositEnabled = settings.OnlineDepositEnabled,
                SalesClosed = settings.SalesClosed,
                SalesClosedMessage = settings.SalesClosedMessage,
                RentalTypeMode = NormalizeRentalTypeMode(settings.RentalTypeMode),
                PromoBanner = BookingEngineCouponHelper.BuildPromoBanner(settings),
                HasActiveCoupons = hasActiveCoupons,
                Hotels = hotels
            };
        }

        private static async Task<BookingEngineSettings?> FindBookingEngineSettingsAsync(
            ApplicationDbContext ctx,
            HotelScope hotelScope,
            bool tracked,
            CancellationToken cancellationToken)
        {
            var query = tracked ? ctx.BookingEngineSettings : ctx.BookingEngineSettings.AsNoTracking();
            return await query.FirstOrDefaultAsync(
                s => s.HotelId == hotelScope.ScopeHotelId || s.HotelId == hotelScope.LocalHotelId,
                cancellationToken);
        }

        private static async Task<BookingEngineSettings> GetOrCreateSettingsTrackedAsync(
            ApplicationDbContext ctx,
            HotelScope hotelScope,
            CancellationToken cancellationToken)
        {
            var settings = await FindBookingEngineSettingsAsync(ctx, hotelScope, tracked: true, cancellationToken);
            if (settings != null)
            {
                if (settings.HotelId == hotelScope.LocalHotelId && hotelScope.LocalHotelId != hotelScope.ScopeHotelId)
                {
                    settings.HotelId = hotelScope.ScopeHotelId;
                    await ctx.SaveChangesAsync(cancellationToken);
                }

                return settings;
            }

            settings = new BookingEngineSettings
            {
                HotelId = hotelScope.ScopeHotelId,
                IsEnabled = true,
                ShowCurrentBranchOnly = true,
                MinimumStayNights = 1,
                DepositMode = "optional",
                CreatedAt = KsaTime.Now
            };
            ctx.BookingEngineSettings.Add(settings);
            await ctx.SaveChangesAsync(cancellationToken);
            return settings;
        }

        private static bool TryParseStayDates(
            BookingSearchRequestDto request,
            BookingEngineSettings? settings,
            out DateTime checkIn,
            out DateTime checkOut,
            out int nights,
            out string error)
        {
            error = string.Empty;
            checkIn = default;
            checkOut = default;
            nights = 1;

            if (!TryParseDateOnly(request.FromDate, out checkIn))
            {
                error = "Invalid check-in date.";
                return false;
            }

            if (!TryParseDateOnly(request.ToDate, out checkOut))
            {
                error = "Invalid check-out date.";
                return false;
            }

            checkIn = checkIn.Date;
            checkOut = checkOut.Date;
            if (checkOut <= checkIn)
            {
                error = "Check-out must be after check-in.";
                return false;
            }

            nights = BookingEnginePricingHelper.CountNights(checkIn, checkOut);
            var minStay = Math.Max(1, settings?.MinimumStayNights ?? 1);
            if (nights < minStay)
            {
                error = $"Minimum stay is {minStay} night(s).";
                return false;
            }

            return true;
        }

        private static bool TryParseDateOnly(string? value, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var s = value.Trim();
            if (DateTime.TryParseExact(s, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date))
            {
                return true;
            }

            return DateTime.TryParse(s, out date);
        }

        private static string NormalizeRental(string? rentalType)
        {
            var v = (rentalType ?? "daily").Trim().ToLowerInvariant();
            return v is "monthly" or "yearly" ? v : "daily";
        }

        private static string NormalizeRentalTypeMode(string? mode)
        {
            var v = (mode ?? "both").Trim().ToLowerInvariant();
            return v switch
            {
                "daily_only" or "dailyonly" or "daily-only" => "daily_only",
                "monthly_only" or "monthlyonly" or "monthly-only" => "monthly_only",
                "hidden" or "hide" or "none" => "hidden",
                _ => "both"
            };
        }

        private static string ResolveRentalForSearch(string? requested, BookingEngineSettings settings)
        {
            var mode = NormalizeRentalTypeMode(settings.RentalTypeMode);
            return mode switch
            {
                "daily_only" => "daily",
                "monthly_only" => "monthly",
                "hidden" => NormalizeRental(requested),
                _ => NormalizeRental(requested)
            };
        }

        /// <summary>
        /// <c>hotel_settings.zaaer_id</c> is the integration hotel id used on
        /// <c>apartments.hotel_id</c>, <c>room_type_rates.hotel_id</c>, taxes, and
        /// <c>booking_engine_settings.hotel_id</c> / <c>booking_engine_media.hotel_id</c>.
        /// <c>hotel_settings.hotel_id</c> is the local PK kept for legacy rows.
        /// </summary>
        private sealed record HotelScope(int ScopeHotelId, int LocalHotelId)
        {
            public static HotelScope From(HotelSettings hotel) =>
                new(hotel.ZaaerId is > 0 ? hotel.ZaaerId.Value : hotel.HotelId, hotel.HotelId);

            public bool Matches(int? entityHotelId) =>
                entityHotelId.HasValue &&
                (entityHotelId.Value == ScopeHotelId || entityHotelId.Value == LocalHotelId);
        }

        private static async Task<BookingEnginePricingHelper.TaxConfig> GetTaxConfigForHotelAsync(
            ApplicationDbContext ctx,
            HotelScope hotelScope,
            CancellationToken cancellationToken)
        {
            var tax = await BookingEnginePricingHelper.GetTaxConfigAsync(ctx, hotelScope.ScopeHotelId, cancellationToken);
            if (tax.VatRate > 0m || tax.EwaRate > 0m)
            {
                return tax;
            }

            return await BookingEnginePricingHelper.GetTaxConfigAsync(ctx, hotelScope.LocalHotelId, cancellationToken);
        }

        /// <summary>
        /// Integration key shared by:
        /// <c>room_types.zaaer_id</c>, <c>apartments.roomtype_id</c>, <c>room_type_rates.roomtype_id</c>.
        /// <c>apartments.zaaer_id</c> is the unit id — not used for type/rate joins.
        /// </summary>
        private static int ResolveApartmentRoomTypeRef(RoomType roomType)
        {
            if (roomType.ZaaerId is > 0)
            {
                return roomType.ZaaerId.Value;
            }

            return roomType.RoomTypeId;
        }

        private static bool IsVacantApartmentStatus(string? status)
        {
            var st = (status ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "").Replace("_", "");
            return st is "vacant" or "available" or "free";
        }

        private async Task<int> CountAvailableUnitsAsync(
            ApplicationDbContext ctx,
            HotelScope hotelScope,
            RoomType roomType,
            DateTime checkIn,
            DateTime checkOut,
            CancellationToken cancellationToken)
        {
            var roomTypeRef = ResolveApartmentRoomTypeRef(roomType);
            var bookableApartments = await LoadBookableApartmentsByRoomTypeRefAsync(
                ctx,
                hotelScope,
                roomTypeRef,
                checkIn,
                checkOut,
                null,
                asNoTracking: true,
                cancellationToken);

            return bookableApartments.Count(apt => IsVacantApartmentStatus(apt.Status));
        }

        private async Task<int> CountAvailableUnitsByRefAsync(
            ApplicationDbContext ctx,
            HotelScope hotelScope,
            int roomTypeRef,
            DateTime checkIn,
            DateTime checkOut,
            CancellationToken cancellationToken)
        {
            var bookableApartments = await LoadBookableApartmentsByRoomTypeRefAsync(
                ctx,
                hotelScope,
                roomTypeRef,
                checkIn,
                checkOut,
                null,
                asNoTracking: true,
                cancellationToken);

            return bookableApartments.Count(apt => IsVacantApartmentStatus(apt.Status));
        }

        private async Task<Apartment?> PickAvailableApartmentAsync(
            ApplicationDbContext ctx,
            HotelScope hotelScope,
            RoomType roomType,
            DateTime checkIn,
            DateTime checkOut,
            CancellationToken cancellationToken)
        {
            var picked = await PickAvailableApartmentsAsync(
                ctx,
                hotelScope,
                roomType,
                checkIn,
                checkOut,
                1,
                new HashSet<int>(),
                cancellationToken);
            return picked.FirstOrDefault();
        }

        private async Task<List<Apartment>> PickAvailableApartmentsAsync(
            ApplicationDbContext ctx,
            HotelScope hotelScope,
            RoomType roomType,
            DateTime checkIn,
            DateTime checkOut,
            int quantity,
            HashSet<int> excludeApartmentIds,
            CancellationToken cancellationToken)
        {
            if (quantity <= 0)
            {
                return new List<Apartment>();
            }

            var roomTypeRef = ResolveApartmentRoomTypeRef(roomType);
            var apartments = await LoadBookableApartmentsByRoomTypeRefAsync(
                ctx,
                hotelScope,
                roomTypeRef,
                checkIn,
                checkOut,
                excludeApartmentIds,
                asNoTracking: false,
                cancellationToken);

            var vacantPool = new List<Apartment>();
            var cleaningPool = new List<Apartment>();

            foreach (var apt in apartments)
            {
                var hk = (apt.HousekeepingStatus ?? string.Empty).Trim().ToLowerInvariant();
                if (hk is "dirty" or "cleaning" or "inspected" or "pendingcleaning")
                {
                    cleaningPool.Add(apt);
                    continue;
                }

                if (IsVacantApartmentStatus(apt.Status))
                {
                    vacantPool.Add(apt);
                }
            }

            var result = new List<Apartment>();
            foreach (var apt in vacantPool.Concat(cleaningPool))
            {
                if (result.Count >= quantity)
                {
                    break;
                }

                result.Add(apt);
                excludeApartmentIds.Add(apt.ApartmentId);
            }

            return result;
        }

        private sealed record ConfirmLinePlan(
            RoomType RoomType,
            IReadOnlyList<Apartment> Apartments,
            (decimal Net, decimal Ewa, decimal Vat, decimal Total) Pricing,
            decimal GrossNight);

        private static async Task<List<Apartment>> LoadBookableApartmentsByRoomTypeRefAsync(
            ApplicationDbContext ctx,
            HotelScope hotelScope,
            int roomTypeRef,
            DateTime checkIn,
            DateTime checkOut,
            HashSet<int>? excludeApartmentIds,
            bool asNoTracking,
            CancellationToken cancellationToken)
        {
            IQueryable<Apartment> query = ctx.Apartments;
            if (asNoTracking)
            {
                query = query.AsNoTracking();
            }

            var apartments = await query
                .Where(a =>
                    a.RoomTypeId == roomTypeRef &&
                    (a.HotelId == hotelScope.ScopeHotelId || a.HotelId == hotelScope.LocalHotelId))
                .OrderBy(a => a.ApartmentCode)
                .ToListAsync(cancellationToken);

            var candidates = apartments
                .Where(apt => excludeApartmentIds == null || !excludeApartmentIds.Contains(apt.ApartmentId))
                .Where(apt =>
                {
                    var st = (apt.Status ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "").Replace("_", "");
                    return st is not ("outoforder" or "ooo" or "maintenance" or "blocked");
                })
                .ToList();

            if (candidates.Count == 0)
            {
                return candidates;
            }

            var apartmentStorageIds = candidates
                .SelectMany(apt =>
                {
                    var ids = new List<int> { apt.ApartmentId };
                    if (apt.ZaaerId is > 0)
                    {
                        ids.Add(apt.ZaaerId.Value);
                    }

                    ids.Add(apt.ZaaerId ?? apt.ApartmentId);
                    return ids;
                })
                .Distinct()
                .ToList();

            var checkInDate = checkIn.Date;
            var checkOutExclusive = checkOut.Date.AddDays(1);

            var maintenanceUnitIds = await ctx.Maintenances.AsNoTracking()
                .Where(m =>
                    (m.HotelId == hotelScope.ScopeHotelId || m.HotelId == hotelScope.LocalHotelId) &&
                    apartmentStorageIds.Contains(m.UnitId) &&
                    m.FromDate < checkOutExclusive &&
                    m.ToDate >= checkInDate &&
                    (m.Status == null || m.Status == "" || m.Status == "active" || m.Status == "maintenance" || m.Status == "open" || m.Status == "inprogress"))
                .Select(m => m.UnitId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var maintenanceUnitSet = maintenanceUnitIds.ToHashSet();

            var overlappingUnits = await ctx.ReservationUnits.AsNoTracking()
                .Where(unit =>
                    apartmentStorageIds.Contains(unit.ApartmentId) &&
                    unit.CheckInDate < checkOutExclusive &&
                    (unit.DepartureDate ?? unit.CheckOutDate) >= checkInDate)
                .Select(unit => new
                {
                    unit.ApartmentId,
                    unit.Status
                })
                .ToListAsync(cancellationToken);

            var reservedApartmentSet = overlappingUnits
                .Where(unit => !string.IsNullOrWhiteSpace(unit.Status) && !IsInactiveUnitStatus(unit.Status))
                .Select(unit => unit.ApartmentId)
                .ToHashSet();

            return candidates
                .Where(apt =>
                {
                    var boardId = apt.ZaaerId ?? apt.ApartmentId;
                    var ids = apt.ZaaerId is > 0
                        ? new[] { boardId, apt.ApartmentId, apt.ZaaerId.Value }
                        : new[] { boardId, apt.ApartmentId };

                    return !ids.Any(id => maintenanceUnitSet.Contains(id) || reservedApartmentSet.Contains(id));
                })
                .ToList();
        }

        private static void SeedReservationUnitDayRates(
            ApplicationDbContext ctx,
            int rateReservationId,
            IReadOnlyList<(ReservationUnit Unit, decimal GrossNight)> units,
            DateTime checkIn,
            DateTime checkOut,
            string rentalNorm,
            BookingEnginePricingHelper.TaxConfig taxConfig)
        {
            if (units.Count == 0)
            {
                return;
            }

            var isMonthly = string.Equals(rentalNorm, "monthly", StringComparison.OrdinalIgnoreCase);
            foreach (var (unit, grossNight) in units)
            {
                if (unit.UnitId <= 0 || grossNight <= 0m)
                {
                    continue;
                }

                if (isMonthly)
                {
                    AddReservationUnitDayRateRow(ctx, rateReservationId, unit.UnitId, checkIn.Date, grossNight, taxConfig);
                    continue;
                }

                for (var night = checkIn.Date; night < checkOut.Date; night = night.AddDays(1))
                {
                    AddReservationUnitDayRateRow(ctx, rateReservationId, unit.UnitId, night, grossNight, taxConfig);
                }
            }
        }

        private static void AddReservationUnitDayRateRow(
            ApplicationDbContext ctx,
            int rateReservationId,
            int unitId,
            DateTime nightDate,
            decimal grossRate,
            BookingEnginePricingHelper.TaxConfig taxConfig)
        {
            var gross = Math.Round(grossRate, 2, MidpointRounding.AwayFromZero);
            var calc = BookingEnginePricingHelper.Calculate(gross, taxConfig);
            ctx.ReservationUnitDayRates.Add(new ReservationUnitDayRate
            {
                ReservationId = rateReservationId,
                UnitId = unitId,
                NightDate = nightDate.Date,
                GrossRate = gross,
                NetAmount = calc.Net,
                EwaAmount = calc.Ewa,
                VatAmount = calc.Vat,
                IsManual = false,
                CreatedAt = KsaTime.Now
            });
        }

        private static List<BookingConfirmLineDto> NormalizeConfirmLines(BookingConfirmRequestDto request)
        {
            if (request.Lines != null && request.Lines.Count > 0)
            {
                return request.Lines
                    .Where(l => l.RoomTypeId > 0 && l.Quantity > 0)
                    .GroupBy(l => l.RoomTypeId)
                    .Select(g => new BookingConfirmLineDto
                    {
                        RoomTypeId = g.Key,
                        Quantity = g.Sum(x => x.Quantity)
                    })
                    .ToList();
            }

            if (request.RoomTypeId > 0)
            {
                return new List<BookingConfirmLineDto>
                {
                    new() { RoomTypeId = request.RoomTypeId, Quantity = 1 }
                };
            }

            return new List<BookingConfirmLineDto>();
        }

        private static async Task<bool> IsApartmentBookableAsync(
            ApplicationDbContext ctx,
            HotelScope hotelScope,
            Apartment apt,
            DateTime checkIn,
            DateTime checkOut,
            CancellationToken cancellationToken)
        {
            var st = (apt.Status ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "").Replace("_", "");
            if (st is "outoforder" or "ooo" or "maintenance" or "blocked")
            {
                return false;
            }

            var boardId = apt.ZaaerId ?? apt.ApartmentId;
            var checkOutExclusive = checkOut.Date.AddDays(1);

            var hasMaintenance = await ctx.Maintenances.AsNoTracking()
                .AnyAsync(m =>
                    (m.HotelId == hotelScope.ScopeHotelId || m.HotelId == hotelScope.LocalHotelId) &&
                    (m.UnitId == boardId || m.UnitId == apt.ApartmentId || (apt.ZaaerId.HasValue && m.UnitId == apt.ZaaerId.Value)) &&
                    m.FromDate < checkOutExclusive &&
                    m.ToDate >= checkIn.Date &&
                    (m.Status == null || m.Status == "" || m.Status == "active" || m.Status == "maintenance" || m.Status == "open" || m.Status == "inprogress"),
                    cancellationToken);

            if (hasMaintenance)
            {
                return false;
            }

            var aptIds = new[] { boardId, apt.ApartmentId };
            var overlappingUnits = await ctx.ReservationUnits.AsNoTracking()
                .Where(unit =>
                    aptIds.Contains(unit.ApartmentId) &&
                    unit.CheckInDate < checkOutExclusive)
                .Select(unit => new { unit.DepartureDate, unit.CheckOutDate, unit.Status })
                .ToListAsync(cancellationToken);

            var hasOverlap = overlappingUnits.Any(unit =>
            {
                var end = unit.DepartureDate ?? unit.CheckOutDate;
                if (end.Date < checkIn.Date)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(unit.Status))
                {
                    return false;
                }

                return !IsInactiveUnitStatus(unit.Status);
            });

            return !hasOverlap;
        }

        private static bool IsInactiveUnitStatus(string status)
        {
            var norm = status.Trim().ToLowerInvariant().Replace("-", "").Replace("_", "");
            return norm is "cancelled" or "canceled" or "checkedout" or "noshow";
        }

        private static async Task<RoomType?> ResolveRoomTypeAsync(
            ApplicationDbContext ctx,
            HotelScope hotelScope,
            int roomTypeRouteId,
            CancellationToken cancellationToken)
        {
            return await ctx.RoomTypes.AsNoTracking()
                .FirstOrDefaultAsync(
                    rt => (rt.HotelId == hotelScope.ScopeHotelId || rt.HotelId == hotelScope.LocalHotelId) &&
                          (rt.RoomTypeId == roomTypeRouteId || rt.ZaaerId == roomTypeRouteId),
                    cancellationToken);
        }

        private static async Task<decimal> ResolveGrossRateByRoomTypeRefOnlyAsync(
            ApplicationDbContext ctx,
            HotelScope hotelScope,
            int roomTypeRateRef,
            RoomType? roomType,
            string rentalNorm,
            DateTime rateDate,
            CancellationToken cancellationToken)
        {
            var rateKeys = roomType != null
                ? RoomTypeRateQueryHelper.BuildRateKeys(roomType)
                : new HashSet<int> { roomTypeRateRef };

            if (!rateKeys.Contains(roomTypeRateRef))
            {
                rateKeys.Add(roomTypeRateRef);
            }

            var internalRoomTypeId = roomType?.RoomTypeId;
            return await RoomTypeRateQueryHelper.ResolveGrossAsync(
                ctx,
                hotelScope.ScopeHotelId,
                hotelScope.LocalHotelId,
                rateKeys,
                internalRoomTypeId,
                rentalNorm,
                rateDate,
                cancellationToken);
        }

        private static async Task<decimal> ResolveGrossRateAsync(
            ApplicationDbContext ctx,
            HotelScope hotelScope,
            int roomTypeRateRef,
            RoomType roomType,
            string rentalNorm,
            DateTime rateDate,
            CancellationToken cancellationToken)
        {
            var fromRates = await ResolveGrossRateByRoomTypeRefOnlyAsync(
                ctx, hotelScope, roomTypeRateRef, roomType, rentalNorm, rateDate, cancellationToken);
            if (fromRates > 0m)
            {
                return fromRates;
            }

            if (rentalNorm == "monthly")
            {
                return roomType.SeasonRate ?? roomType.BaseRate ?? 0m;
            }

            return roomType.BaseRate ?? roomType.SeasonRate ?? 0m;
        }

        private static async Task<IReadOnlyList<string>> LoadRoomImagesAsync(
            ApplicationDbContext ctx,
            HotelScope hotelScope,
            RoomType roomType,
            CancellationToken cancellationToken)
        {
            var rtId = roomType.ZaaerId ?? roomType.RoomTypeId;
            var media = await ctx.BookingEngineMedia.AsNoTracking()
                .Where(m =>
                    (m.HotelId == hotelScope.LocalHotelId || m.HotelId == hotelScope.ScopeHotelId) &&
                    (m.RoomTypeId == null || m.RoomTypeId == rtId || m.RoomTypeId == roomType.RoomTypeId))
                .OrderByDescending(m => m.IsPrimary)
                .ThenBy(m => m.SortOrder)
                .Select(m => m.ImageUrl)
                .ToListAsync(cancellationToken);

            if (media.Count > 0)
            {
                return media;
            }

            if (!string.IsNullOrWhiteSpace(roomType.ImageUrl))
            {
                return new[] { roomType.ImageUrl };
            }

            return Array.Empty<string>();
        }

        private static IReadOnlyList<string> BuildHighlights(RoomType rt)
        {
            var list = new List<string>();
            if (rt.RoomCount > 0)
            {
                list.Add($"{rt.RoomCount} units");
            }

            if (!string.IsNullOrWhiteSpace(rt.RoomCategory))
            {
                list.Add(rt.RoomCategory);
            }

            return list;
        }

        private static IReadOnlyList<BookingOfferFacilityDto> BuildOfferFacilities(
            RoomType rt,
            IReadOnlyList<Apartment> units,
            IReadOnlyDictionary<int, Facility> facilityByZaaer)
        {
            var list = new List<BookingOfferFacilityDto>();
            if (units.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(rt.RoomCategory))
                {
                    list.Add(new BookingOfferFacilityDto
                    {
                        Label = rt.RoomCategory,
                        LabelEn = rt.RoomCategory
                    });
                }

                return list;
            }

            var maxBaths = units.Max(u => u.BathroomsCount ?? 0);
            if (maxBaths > 0)
            {
                list.Add(new BookingOfferFacilityDto
                {
                    Label = maxBaths > 1 ? $"حمام ({maxBaths})" : "حمام خاص",
                    LabelEn = maxBaths > 1 ? $"{maxBaths} bathrooms" : "Private bathroom"
                });
            }

            var kitchen = units
                .Select(u => u.KitchenType)
                .FirstOrDefault(k => !string.IsNullOrWhiteSpace(k) && !string.Equals(k, PropertyKitchenTypes.None, StringComparison.OrdinalIgnoreCase));
            if (kitchen != null)
            {
                list.Add(MapKitchenFacility(kitchen));
            }

            var hall = units
                .Select(u => u.HallType)
                .FirstOrDefault(h => !string.IsNullOrWhiteSpace(h) && !string.Equals(h, PropertyHallTypes.None, StringComparison.OrdinalIgnoreCase));
            if (hall != null)
            {
                list.Add(MapHallFacility(hall));
            }

            var maxArea = units.Where(u => u.Area > 0).Select(u => u.Area).DefaultIfEmpty().Max();
            if (maxArea > 0)
            {
                list.Add(new BookingOfferFacilityDto
                {
                    Label = $"مساحة {maxArea:0.##} م²",
                    LabelEn = $"{maxArea:0.##} m²"
                });
            }

            var seenFacility = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var apt in units)
            {
                foreach (var zaaerId in DeserializeFacilityZaaerIds(apt.FacilitiesJson))
                {
                    if (!facilityByZaaer.TryGetValue(zaaerId, out var facility))
                    {
                        continue;
                    }

                    var key = facility.FacilityName;
                    if (!seenFacility.Add(key))
                    {
                        continue;
                    }

                    list.Add(new BookingOfferFacilityDto
                    {
                        Label = facility.FacilityName,
                        LabelEn = string.IsNullOrWhiteSpace(facility.FacilityNameEn)
                            ? facility.FacilityName
                            : facility.FacilityNameEn
                    });
                }
            }

            return list.Take(12).ToList();
        }

        private static readonly string[] DefaultBookingServices = { "wifi", "ac", "tv", "safe" };

        private static readonly string[] BookingServiceDisplayOrder =
        {
            "wifi", "ac", "tv", "safe", "minibar", "balcony", "parking"
        };

        private static IReadOnlyList<string> BuildOfferServices(IReadOnlyList<Apartment> units)
        {
            var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var apt in units)
            {
                foreach (var code in DeserializeServices(apt.ServicesJson))
                {
                    if (string.IsNullOrWhiteSpace(code))
                    {
                        continue;
                    }

                    merged.Add(code.Trim().ToLowerInvariant());
                }
            }

            if (merged.Count == 0)
            {
                return DefaultBookingServices;
            }

            var ordered = BookingServiceDisplayOrder.Where(merged.Contains).ToList();
            foreach (var extra in merged.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                if (!ordered.Contains(extra, StringComparer.OrdinalIgnoreCase))
                {
                    ordered.Add(extra);
                }
            }

            return ordered;
        }

        private static List<string> DeserializeServices(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<string>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static BookingOfferFacilityDto MapKitchenFacility(string kitchenType)
        {
            return kitchenType.Trim().ToLowerInvariant() switch
            {
                PropertyKitchenTypes.Small => new() { Label = "مطبخ صغير", LabelEn = "Small kitchen" },
                PropertyKitchenTypes.Standard => new() { Label = "مطبخ", LabelEn = "Kitchen" },
                PropertyKitchenTypes.Medium => new() { Label = "مطبخ متوسط", LabelEn = "Medium kitchen" },
                PropertyKitchenTypes.Large => new() { Label = "مطبخ كبير", LabelEn = "Large kitchen" },
                _ => new() { Label = "مطبخ", LabelEn = "Kitchen" }
            };
        }

        private static BookingOfferFacilityDto MapHallFacility(string hallType)
        {
            return hallType.Trim().ToLowerInvariant() switch
            {
                PropertyHallTypes.Small => new() { Label = "صالة صغيرة", LabelEn = "Small lounge" },
                PropertyHallTypes.Medium => new() { Label = "صالة", LabelEn = "Lounge" },
                PropertyHallTypes.Large => new() { Label = "صالة واسعة", LabelEn = "Large lounge" },
                PropertyHallTypes.Deluxe => new() { Label = "صالة فاخرة", LabelEn = "Deluxe lounge" },
                _ => new() { Label = "صالة", LabelEn = "Lounge / living area" }
            };
        }

        private static List<int> DeserializeFacilityZaaerIds(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<int>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<int>>(json) ?? new List<int>();
            }
            catch
            {
                return new List<int>();
            }
        }

        private static decimal CalculateDepositDue(BookingEngineSettings settings, decimal total, bool payNow)
        {
            if (!payNow)
            {
                return 0m;
            }

            var mode = (settings.DepositMode ?? "optional").Trim().ToLowerInvariant();
            if (mode is "none")
            {
                return 0m;
            }

            if (settings.DepositAmount is > 0m)
            {
                return Math.Min(total, settings.DepositAmount.Value);
            }

            if (settings.DepositPercent is > 0m)
            {
                return Math.Round(total * settings.DepositPercent.Value / 100m, 2, MidpointRounding.AwayFromZero);
            }

            return mode == "required" ? total : Math.Round(total * 0.2m, 2, MidpointRounding.AwayFromZero);
        }

        private static string ResolveHeaderStatus(BookingEngineSettings settings, bool payNow, decimal depositDue, decimal total)
        {
            var mode = (settings.DepositMode ?? "optional").Trim().ToLowerInvariant();
            if (mode == "required" && depositDue < total)
            {
                return "unconfirmed";
            }

            if (payNow && depositDue > 0m)
            {
                return "confirmed";
            }

            return "unconfirmed";
        }

        private static string BuildPublicUrl(string slugOrCode)
        {
            var slug = (slugOrCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(slug))
            {
                return "/booking-engine.html";
            }

            return $"/booking-engine.html?hotel={Uri.EscapeDataString(slug)}";
        }

        private static BookingConfirmResponseDto Fail(string message) =>
            new() { Success = false, Message = message };

        private async Task<(Customer Customer, long? NewCustomerAuditId)> ResolveOrCreateBookingCustomerAsync(
            ApplicationDbContext ctx,
            HotelScope hotelScope,
            BookingConfirmRequestDto request,
            string customerName,
            CancellationToken cancellationToken)
        {
            var normalizedMobile = PhoneNumberNormalizer.NormalizeMobileForStorage(request.Phone);
            if (!string.IsNullOrWhiteSpace(normalizedMobile))
            {
                var existing = await FindCustomerByNormalizedPhoneAsync(
                    ctx,
                    hotelScope.ScopeHotelId,
                    normalizedMobile,
                    cancellationToken);

                if (existing != null)
                {
                    ApplyBookingGuestUpdates(existing, request);
                    existing.UpdatedAt = KsaTime.Now;
                    await ctx.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation(
                        "Booking engine reusing customer {CustomerId} for mobile {Mobile}",
                        existing.CustomerId,
                        normalizedMobile);
                    return (existing, null);
                }
            }

            var customerIdentity = await _numberingService.GetNextBusinessIdentityAsync(
                "customer",
                hotelScope.ScopeHotelId,
                null,
                $"booking-customer:{hotelScope.ScopeHotelId}:{Guid.NewGuid():N}",
                cancellationToken);

            var customerZaaerId = ZaaerIdMapper.ToNullableInt32(customerIdentity.ZaaerId);

            var customer = new Customer
            {
                CustomerNo = customerIdentity.DocumentNo,
                CustomerName = customerName,
                MobileNo = normalizedMobile,
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                NId = request.NationalityId,
                Comments = string.IsNullOrWhiteSpace(request.Notes) ? SourceBookingEngine : $"{SourceBookingEngine}: {request.Notes.Trim()}",
                HotelId = hotelScope.ScopeHotelId,
                ZaaerId = customerZaaerId,
                IsActive = true,
                CreatedAt = KsaTime.Now
            };
            ctx.Customers.Add(customer);
            await ctx.SaveChangesAsync(cancellationToken);
            return (customer, customerIdentity.AuditId);
        }

        private static void ApplyBookingGuestUpdates(Customer customer, BookingConfirmRequestDto request)
        {
            if (!string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(customer.Email))
            {
                customer.Email = request.Email.Trim();
            }

            if (request.NationalityId.HasValue && !customer.NId.HasValue)
            {
                customer.NId = request.NationalityId;
            }
        }

        private static async Task<Customer?> FindCustomerByNormalizedPhoneAsync(
            ApplicationDbContext ctx,
            int hotelId,
            string normalizedPhone,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(normalizedPhone))
            {
                return null;
            }

            var suffix = normalizedPhone.Length >= 9 ? normalizedPhone[^9..] : normalizedPhone;

            var candidates = await ctx.Customers
                .Where(c =>
                    c.HotelId == hotelId &&
                    c.IsActive &&
                    c.MobileNo != null &&
                    c.MobileNo.Length >= suffix.Length &&
                    c.MobileNo.EndsWith(suffix))
                .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
                .ThenByDescending(c => c.CustomerId)
                .Take(12)
                .ToListAsync(cancellationToken);

            return candidates.FirstOrDefault(c =>
                string.Equals(
                    PhoneNumberNormalizer.NormalizeMobileForStorage(c.MobileNo),
                    normalizedPhone,
                    StringComparison.Ordinal));
        }

        private static void SplitCustomerName(string? fullName, out string? firstName, out string? lastName)
        {
            firstName = null;
            lastName = null;
            var name = (fullName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                firstName = parts[0];
                return;
            }

            lastName = parts[^1];
            firstName = string.Join(' ', parts.Take(parts.Length - 1));
        }
    }
}
