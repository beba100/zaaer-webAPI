#pragma warning disable CS1591

using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.BookingEngine;
using zaaerIntegration.Services.BookingEngine;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    public sealed partial class BookingEngineService
    {
        public async Task<BookingCouponValidateResponseDto?> ValidateCouponAsync(
            BookingCouponValidateRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var (tenant, ctx, hotel) = await OpenHotelAsync(request.HotelCode ?? string.Empty, cancellationToken);
            if (tenant == null || ctx == null || hotel == null)
            {
                return new BookingCouponValidateResponseDto { Valid = false, Message = "Hotel not found." };
            }

            await using (ctx)
            {
                var hotelScope = HotelScope.From(hotel);
                var settings = await FindBookingEngineSettingsAsync(ctx, hotelScope, tracked: false, cancellationToken);
                if (settings != null && (!settings.IsEnabled || settings.SalesClosed))
                {
                    return new BookingCouponValidateResponseDto { Valid = false, Message = "Booking is not available." };
                }

                settings ??= await GetOrCreateSettingsTrackedAsync(ctx, hotelScope, cancellationToken);

                if (!TryParseStayDates(
                        new BookingSearchRequestDto
                        {
                            FromDate = request.FromDate,
                            ToDate = request.ToDate,
                            RentalType = request.RentalType
                        },
                        settings,
                        out var checkIn,
                        out _,
                        out var nights,
                        out var dateError))
                {
                    return new BookingCouponValidateResponseDto { Valid = false, Message = dateError };
                }

                var grandTotal = await ComputeCartGrandTotalAsync(
                    ctx,
                    hotelScope,
                    settings,
                    request,
                    cancellationToken);

                if (grandTotal <= 0m)
                {
                    return new BookingCouponValidateResponseDto { Valid = false, Message = "Select rooms first." };
                }

                var coupon = await BookingEngineCouponHelper.FindActiveCouponAsync(
                    ctx,
                    hotelScope.ScopeHotelId,
                    request.CouponCode ?? string.Empty,
                    cancellationToken);

                if (coupon == null)
                {
                    return new BookingCouponValidateResponseDto { Valid = false, Message = "Invalid coupon code." };
                }

                var roomTypeIds = NormalizeConfirmLines(ToConfirmRequest(request)).Select(l => l.RoomTypeId).ToList();
                return BookingEngineCouponHelper.BuildValidateResponse(
                    coupon,
                    grandTotal,
                    nights,
                    roomTypeIds,
                    checkIn);
            }
        }

        public async Task<IReadOnlyList<BookingEngineCouponDto>> ListCouponsAsync(
            int hotelId,
            CancellationToken cancellationToken = default)
        {
            await using var ctx = OpenAdminContext();
            var list = await ctx.BookingEngineCoupons.AsNoTracking()
                .Where(c => c.HotelId == hotelId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
            return list.Select(BookingEngineCouponHelper.MapCoupon).ToList();
        }

        public async Task<BookingEngineCouponDto> CreateCouponAsync(
            int hotelId,
            BookingEngineCouponUpsertDto dto,
            CancellationToken cancellationToken = default)
        {
            ValidateCouponUpsert(dto);

            await using var ctx = OpenAdminContext();
            var promo = BookingEngineCouponHelper.NormalizePromoCode(dto.PromoCode);
            var exists = await ctx.BookingEngineCoupons.AnyAsync(
                c => c.HotelId == hotelId && c.PromoCode == promo,
                cancellationToken);
            if (exists)
            {
                throw new InvalidOperationException("Promo code already exists for this hotel.");
            }

            var identity = await _numberingService.GetNextBusinessIdentityAsync(
                BookingEngineCouponHelper.DocCode,
                hotelId,
                null,
                $"booking-coupon:{hotelId}:{Guid.NewGuid():N}",
                cancellationToken);

            var entity = MapCouponUpsert(new BookingEngineCoupon
            {
                HotelId = hotelId,
                CouponNo = identity.DocumentNo,
                PromoCode = promo,
                CreatedAt = KsaTime.Now
            }, dto);

            ctx.BookingEngineCoupons.Add(entity);
            await ctx.SaveChangesAsync(cancellationToken);
            await _numberingService.MarkCommittedAsync(identity.AuditId, cancellationToken);
            return BookingEngineCouponHelper.MapCoupon(entity);
        }

        public async Task<BookingEngineCouponDto?> UpdateCouponAsync(
            int hotelId,
            int couponId,
            BookingEngineCouponUpsertDto dto,
            CancellationToken cancellationToken = default)
        {
            ValidateCouponUpsert(dto);

            await using var ctx = OpenAdminContext();
            var entity = await ctx.BookingEngineCoupons.FirstOrDefaultAsync(
                c => c.CouponId == couponId && c.HotelId == hotelId,
                cancellationToken);
            if (entity == null)
            {
                return null;
            }

            var promo = BookingEngineCouponHelper.NormalizePromoCode(dto.PromoCode);
            var duplicate = await ctx.BookingEngineCoupons.AnyAsync(
                c => c.HotelId == hotelId && c.PromoCode == promo && c.CouponId != couponId,
                cancellationToken);
            if (duplicate)
            {
                throw new InvalidOperationException("Promo code already exists for this hotel.");
            }

            entity = MapCouponUpsert(entity, dto);
            entity.PromoCode = promo;
            entity.UpdatedAt = KsaTime.Now;
            await ctx.SaveChangesAsync(cancellationToken);
            return BookingEngineCouponHelper.MapCoupon(entity);
        }

        public async Task<bool> DeleteCouponAsync(int hotelId, int couponId, CancellationToken cancellationToken = default)
        {
            await using var ctx = OpenAdminContext();
            var entity = await ctx.BookingEngineCoupons.FirstOrDefaultAsync(
                c => c.CouponId == couponId && c.HotelId == hotelId,
                cancellationToken);
            if (entity == null)
            {
                return false;
            }

            ctx.BookingEngineCoupons.Remove(entity);
            await ctx.SaveChangesAsync(cancellationToken);
            return true;
        }

        private async Task<decimal> ComputeCartGrandTotalAsync(
            ApplicationDbContext ctx,
            HotelScope hotelScope,
            BookingEngineSettings settings,
            BookingCouponValidateRequestDto request,
            CancellationToken cancellationToken)
        {
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
                    out _))
            {
                return 0m;
            }

            var rentalNorm = ResolveRentalForSearch(request.RentalType, settings);
            var taxConfig = await GetTaxConfigForHotelAsync(ctx, hotelScope, cancellationToken);
            var scale = rentalNorm == "monthly" ? 1 : nights;
            var lines = NormalizeConfirmLines(ToConfirmRequest(request));
            decimal total = 0m;

            foreach (var line in lines)
            {
                var roomType = await ResolveRoomTypeAsync(ctx, hotelScope, line.RoomTypeId, cancellationToken);
                if (roomType == null)
                {
                    continue;
                }

                var roomTypeRateRef = ResolveApartmentRoomTypeRef(roomType);
                var grossNight = await ResolveGrossRateAsync(ctx, hotelScope, roomTypeRateRef, roomType, rentalNorm, checkIn.Date, cancellationToken);
                if (grossNight <= 0m)
                {
                    continue;
                }

                var calc = BookingEnginePricingHelper.Calculate(grossNight * scale, taxConfig);
                total += calc.Total * line.Quantity;
            }

            return Math.Round(total, 2, MidpointRounding.AwayFromZero);
        }

        private static void ValidateCouponUpsert(BookingEngineCouponUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.PromoCode))
            {
                throw new ArgumentException("Promo code is required.");
            }

            if (dto.DiscountValue <= 0m)
            {
                throw new ArgumentException("Discount value must be greater than zero.");
            }

            var type = (dto.DiscountType ?? "percent").Trim().ToLowerInvariant();
            if (type is not ("percent" or "fixed"))
            {
                throw new ArgumentException("Discount type must be percent or fixed.");
            }

            if (type == "percent" && dto.DiscountValue > 100m)
            {
                throw new ArgumentException("Percent discount cannot exceed 100.");
            }
        }

        private static BookingEngineCoupon MapCouponUpsert(BookingEngineCoupon entity, BookingEngineCouponUpsertDto dto)
        {
            entity.Title = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title.Trim();
            entity.DiscountType = (dto.DiscountType ?? "percent").Trim().ToLowerInvariant();
            entity.DiscountValue = dto.DiscountValue;
            entity.MinStayNights = dto.MinStayNights;
            entity.MinBookingAmount = dto.MinBookingAmount;
            entity.MaxRedemptions = dto.MaxRedemptions;
            entity.ValidFrom = dto.ValidFrom?.Date;
            entity.ValidTo = dto.ValidTo?.Date;
            entity.RoomTypeIds = string.IsNullOrWhiteSpace(dto.RoomTypeIds) ? null : dto.RoomTypeIds.Trim();
            entity.IsActive = dto.IsActive;
            entity.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
            return entity;
        }

        private async Task<(BookingEngineCoupon? Coupon, decimal Discount, string? Error)> ResolveCouponForConfirmAsync(
            ApplicationDbContext ctx,
            HotelScope hotelScope,
            BookingConfirmRequestDto request,
            decimal grandTotal,
            int nights,
            DateTime checkIn,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.CouponCode))
            {
                return (null, 0m, null);
            }

            var coupon = await BookingEngineCouponHelper.FindActiveCouponAsync(
                ctx,
                hotelScope.ScopeHotelId,
                request.CouponCode,
                cancellationToken);

            if (coupon == null)
            {
                return (null, 0m, "Invalid coupon code.");
            }

            var roomTypeIds = NormalizeConfirmLines(request).Select(l => l.RoomTypeId).ToList();
            var error = BookingEngineCouponHelper.ValidateCouponForBooking(
                coupon,
                grandTotal,
                nights,
                roomTypeIds,
                checkIn);

            if (error != null)
            {
                return (null, 0m, error);
            }

            var discount = BookingEngineCouponHelper.CalculateDiscountAmount(coupon, grandTotal);
            return (coupon, discount, null);
        }

        private static BookingConfirmRequestDto ToConfirmRequest(BookingCouponValidateRequestDto request) =>
            new()
            {
                HotelCode = request.HotelCode,
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                RentalType = request.RentalType,
                Lines = request.Lines
            };
    }
}
