using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.BookingEngine;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.BookingEngine
{
    internal static class BookingEngineCouponHelper
    {
        internal const string DocCode = "booking_coupon";

        internal static string NormalizePromoCode(string? code) =>
            (code ?? string.Empty).Trim().ToUpperInvariant();

        internal static BookingEngineCouponDto MapCoupon(BookingEngineCoupon c) =>
            new()
            {
                CouponId = c.CouponId,
                HotelId = c.HotelId,
                CouponNo = c.CouponNo,
                PromoCode = c.PromoCode,
                Title = c.Title,
                DiscountType = c.DiscountType,
                DiscountValue = c.DiscountValue,
                MinStayNights = c.MinStayNights,
                MinBookingAmount = c.MinBookingAmount,
                MaxRedemptions = c.MaxRedemptions,
                RedemptionCount = c.RedemptionCount,
                ValidFrom = c.ValidFrom,
                ValidTo = c.ValidTo,
                RoomTypeIds = c.RoomTypeIds,
                IsActive = c.IsActive,
                Notes = c.Notes
            };

        internal static async Task<BookingEngineCoupon?> FindActiveCouponAsync(
            ApplicationDbContext ctx,
            int hotelId,
            string promoCode,
            CancellationToken cancellationToken)
        {
            var norm = NormalizePromoCode(promoCode);
            if (string.IsNullOrWhiteSpace(norm))
            {
                return null;
            }

            return await ctx.BookingEngineCoupons
                .FirstOrDefaultAsync(
                    c => c.HotelId == hotelId && c.PromoCode == norm && c.IsActive,
                    cancellationToken);
        }

        internal static async Task<bool> HasAnyPublicCouponAsync(
            ApplicationDbContext ctx,
            int hotelId,
            CancellationToken cancellationToken)
        {
            var today = KsaTime.Now.Date;
            return await ctx.BookingEngineCoupons.AsNoTracking()
                .AnyAsync(
                    c =>
                        c.HotelId == hotelId &&
                        c.IsActive &&
                        (!c.ValidFrom.HasValue || c.ValidFrom.Value.Date <= today) &&
                        (!c.ValidTo.HasValue || c.ValidTo.Value.Date >= today) &&
                        (!c.MaxRedemptions.HasValue || c.RedemptionCount < c.MaxRedemptions.Value),
                    cancellationToken);
        }

        internal static string? ValidateCouponForBooking(
            BookingEngineCoupon coupon,
            decimal bookingGrandTotal,
            int nights,
            IReadOnlyCollection<int> roomTypeIdsInCart,
            DateTime checkInDate)
        {
            var today = checkInDate.Date;

            if (coupon.ValidFrom.HasValue && today < coupon.ValidFrom.Value.Date)
            {
                return "Coupon is not valid yet.";
            }

            if (coupon.ValidTo.HasValue && today > coupon.ValidTo.Value.Date)
            {
                return "Coupon has expired.";
            }

            if (coupon.MaxRedemptions.HasValue && coupon.RedemptionCount >= coupon.MaxRedemptions.Value)
            {
                return "Coupon usage limit reached.";
            }

            if (coupon.MinStayNights.HasValue && nights < coupon.MinStayNights.Value)
            {
                return $"Minimum stay is {coupon.MinStayNights.Value} night(s).";
            }

            if (coupon.MinBookingAmount.HasValue && bookingGrandTotal < coupon.MinBookingAmount.Value)
            {
                return $"Minimum booking amount is {coupon.MinBookingAmount.Value:0.##}.";
            }

            var allowed = ParseRoomTypeIds(coupon.RoomTypeIds);
            if (allowed.Count > 0 && roomTypeIdsInCart.Count > 0)
            {
                var anyMatch = roomTypeIdsInCart.Any(id => allowed.Contains(id));
                if (!anyMatch)
                {
                    return "Coupon does not apply to selected room types.";
                }
            }

            return null;
        }

        internal static decimal CalculateDiscountAmount(BookingEngineCoupon coupon, decimal grandTotal)
        {
            if (grandTotal <= 0m)
            {
                return 0m;
            }

            var type = (coupon.DiscountType ?? "percent").Trim().ToLowerInvariant();
            decimal discount;
            if (type == "fixed")
            {
                discount = coupon.DiscountValue;
            }
            else
            {
                discount = Math.Round(grandTotal * coupon.DiscountValue / 100m, 2, MidpointRounding.AwayFromZero);
            }

            return Math.Min(grandTotal, Math.Max(0m, discount));
        }

        internal static BookingCouponValidateResponseDto BuildValidateResponse(
            BookingEngineCoupon coupon,
            decimal bookingGrandTotal,
            int nights,
            IReadOnlyCollection<int> roomTypeIdsInCart,
            DateTime checkInDate)
        {
            var error = ValidateCouponForBooking(coupon, bookingGrandTotal, nights, roomTypeIdsInCart, checkInDate);
            if (error != null)
            {
                return new BookingCouponValidateResponseDto { Valid = false, Message = error };
            }

            var discount = CalculateDiscountAmount(coupon, bookingGrandTotal);
            return new BookingCouponValidateResponseDto
            {
                Valid = true,
                PromoCode = coupon.PromoCode,
                Title = coupon.Title,
                DiscountType = coupon.DiscountType,
                DiscountValue = coupon.DiscountValue,
                DiscountAmount = discount,
                GrandTotalBefore = bookingGrandTotal,
                GrandTotalAfter = Math.Round(bookingGrandTotal - discount, 2, MidpointRounding.AwayFromZero),
                Message = "Coupon applied."
            };
        }

        internal static HashSet<int> ParseRoomTypeIds(string? csv)
        {
            var set = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(csv))
            {
                return set;
            }

            foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, out var id))
                {
                    set.Add(id);
                }
            }

            return set;
        }

        internal static PublicPromoBannerDto? BuildPromoBanner(BookingEngineSettings? settings)
        {
            if (settings == null || !settings.PromoBannerEnabled)
            {
                return null;
            }

            if (settings.PromoBannerEndsAt.HasValue && settings.PromoBannerEndsAt.Value < KsaTime.Now)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(settings.PromoBannerHtml) &&
                string.IsNullOrWhiteSpace(settings.PromoBannerImageUrl))
            {
                return null;
            }

            return new PublicPromoBannerDto
            {
                ImageUrl = settings.PromoBannerImageUrl,
                Html = settings.PromoBannerHtml,
                EndsAt = settings.PromoBannerEndsAt
            };
        }
    }
}
