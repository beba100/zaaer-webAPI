using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;

namespace zaaerIntegration.Utilities
{
    public static class RoomTypeGrossRateResolver
    {
        public static int ResolveStorageRoomTypeId(RoomType roomType) =>
            roomType.ZaaerId is > 0 ? roomType.ZaaerId.Value : roomType.RoomTypeId;

        public static async Task<(decimal Gross, RoomTypeGrossRateSource Source)> ResolveAsync(
            ApplicationDbContext ctx,
            int scopeHotelId,
            int? localHotelId,
            HashSet<int> rateKeys,
            RoomTypeRate? baseRate,
            RoomType? roomTypeFallback,
            string rentalTypeNormalized,
            DateTime? rateDate,
            RoomTypeGrossRateOptions? options,
            CancellationToken cancellationToken = default)
        {
            options ??= RoomTypeGrossRateOptions.Standard;
            var hotelIds = BuildHotelIds(scopeHotelId, localHotelId);

            if (rentalTypeNormalized == "monthly")
            {
                if (baseRate != null)
                {
                    var monthly = baseRate.MonthlyRateMin ?? baseRate.MonthlyRate ?? 0m;
                    if (monthly > 0m)
                    {
                        return (monthly, RoomTypeGrossRateSource.BaseRates);
                    }
                }

                return ResolveRoomTypeAndProgrammaticFallback(
                    roomTypeFallback,
                    scopeHotelId,
                    rateKeys,
                    rateDate,
                    options,
                    preferMonthly: true);
            }

            if (rateDate.HasValue && rateKeys.Count > 0)
            {
                var day = rateDate.Value.Date;
                var daily = await ctx.RoomTypeDailyRates.AsNoTracking()
                    .FirstOrDefaultAsync(
                        d => hotelIds.Contains(d.HotelId) &&
                             rateKeys.Contains(d.RoomTypeId) &&
                             d.RateDate == day,
                        cancellationToken);

                if (daily != null && daily.GrossRate > 0m)
                {
                    return (daily.GrossRate, RoomTypeGrossRateSource.DailyOverride);
                }

                if (baseRate != null)
                {
                    var fromBase = RoomTypeRateResolver.ResolveBaseDailyGross(baseRate, day);
                    if (fromBase > 0m)
                    {
                        return (fromBase, RoomTypeGrossRateSource.BaseRates);
                    }
                }

                return ResolveRoomTypeAndProgrammaticFallback(
                    roomTypeFallback,
                    scopeHotelId,
                    rateKeys,
                    day,
                    options,
                    preferMonthly: false);
            }

            if (baseRate != null)
            {
                var flat = baseRate.DailyRateMin ?? baseRate.DailyRateLowWeekdays ?? baseRate.DailyRateHighWeekdays ?? 0m;
                if (flat > 0m)
                {
                    return (flat, RoomTypeGrossRateSource.BaseRates);
                }
            }

            return ResolveRoomTypeAndProgrammaticFallback(
                roomTypeFallback,
                scopeHotelId,
                rateKeys,
                rateDate,
                options,
                preferMonthly: false);
        }

        private static (decimal Gross, RoomTypeGrossRateSource Source) ResolveRoomTypeAndProgrammaticFallback(
            RoomType? roomType,
            int scopeHotelId,
            HashSet<int> rateKeys,
            DateTime? rateDate,
            RoomTypeGrossRateOptions options,
            bool preferMonthly)
        {
            if (roomType != null)
            {
                if (preferMonthly)
                {
                    var monthly = roomType.SeasonRate ?? roomType.BaseRate ?? 0m;
                    if (monthly > 0m)
                    {
                        return (monthly, RoomTypeGrossRateSource.RoomTypeFallback);
                    }
                }
                else
                {
                    var daily = roomType.BaseRate ?? roomType.SeasonRate ?? 0m;
                    if (daily > 0m)
                    {
                        return (daily, RoomTypeGrossRateSource.RoomTypeFallback);
                    }
                }
            }

            if (options.UseProgrammaticFallback && rateDate.HasValue)
            {
                var roomRef = rateKeys.Count > 0 ? rateKeys.Max() : 0;
                var gross = ResolveProgrammaticFallback(scopeHotelId, roomRef, rateDate.Value, options);
                if (gross > 0m)
                {
                    return (gross, RoomTypeGrossRateSource.ProgrammaticFallback);
                }
            }

            return (0m, RoomTypeGrossRateSource.None);
        }

        public static decimal ResolveProgrammaticFallback(
            int hotelId,
            int roomTypeRef,
            DateTime date,
            RoomTypeGrossRateOptions options)
        {
            var min = options.FallbackMin ?? 80m;
            var max = options.FallbackMax ?? 350m;
            if (max < min)
            {
                (min, max) = (max, min);
            }

            if (max <= min)
            {
                return min;
            }

            var seed = HashCode.Combine(hotelId, roomTypeRef, date.Date.ToOADate());
            var rng = new Random(seed);
            var span = (double)(max - min);
            return Math.Round(min + (decimal)rng.NextDouble() * (decimal)span, 0, MidpointRounding.AwayFromZero);
        }

        private static HashSet<int> BuildHotelIds(int scopeHotelId, int? localHotelId)
        {
            var hotelIds = new HashSet<int> { scopeHotelId };
            if (localHotelId.HasValue && localHotelId.Value > 0 && localHotelId.Value != scopeHotelId)
            {
                hotelIds.Add(localHotelId.Value);
            }

            return hotelIds;
        }
    }
}
