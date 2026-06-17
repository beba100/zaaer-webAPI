using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;

namespace zaaerIntegration.Utilities
{
    /// <summary>
    /// Loads effective gross rates from <c>room_type_rates</c> and <c>room_type_daily_rates</c>.
    /// </summary>
    public static class RoomTypeRateQueryHelper
    {
        public static async Task<decimal> ResolveGrossAsync(
            ApplicationDbContext ctx,
            int scopeHotelId,
            int? localHotelId,
            HashSet<int> rateKeys,
            int? internalRoomTypeId,
            string rentalTypeNormalized,
            DateTime? rateDate,
            CancellationToken cancellationToken)
        {
            var hotelIds = new HashSet<int> { scopeHotelId };
            if (localHotelId.HasValue && localHotelId.Value > 0 && localHotelId.Value != scopeHotelId)
            {
                hotelIds.Add(localHotelId.Value);
            }

            var rates = await ctx.RoomTypeRates.AsNoTracking()
                .Where(r => hotelIds.Contains(r.HotelId))
                .ToListAsync(cancellationToken);

            var rate = rates.FirstOrDefault(r => RoomTypeRateResolver.RateMatchesRoomType(r, rateKeys));
            RoomType? roomType = null;
            if (internalRoomTypeId.HasValue)
            {
                roomType = await ctx.RoomTypes.AsNoTracking()
                    .FirstOrDefaultAsync(rt => rt.RoomTypeId == internalRoomTypeId.Value, cancellationToken);
            }

            var (gross, _) = await RoomTypeGrossRateResolver.ResolveAsync(
                ctx,
                scopeHotelId,
                localHotelId,
                rateKeys,
                rate,
                roomType,
                rentalTypeNormalized,
                rateDate,
                RoomTypeGrossRateOptions.Standard,
                cancellationToken);

            return gross;
        }

        public static async Task<(HashSet<int> RateKeys, int? InternalRoomTypeId)> BuildRateKeysForApartmentAsync(
            ApplicationDbContext ctx,
            Apartment apartment,
            int hotelId,
            CancellationToken cancellationToken)
        {
            var rateKeys = new HashSet<int>();
            int? internalRoomTypeId = null;

            if (!apartment.RoomTypeId.HasValue || apartment.RoomTypeId.Value <= 0)
            {
                return (rateKeys, null);
            }

            var apartmentRt = apartment.RoomTypeId.Value;
            rateKeys.Add(apartmentRt);

            var roomTypeRows = await ctx.RoomTypes.AsNoTracking()
                .Where(rt => rt.HotelId == hotelId && (rt.RoomTypeId == apartmentRt || rt.ZaaerId == apartmentRt))
                .Select(rt => new { rt.RoomTypeId, rt.ZaaerId })
                .ToListAsync(cancellationToken);

            foreach (var row in roomTypeRows)
            {
                internalRoomTypeId = row.RoomTypeId;
                rateKeys.Add(row.RoomTypeId);
                if (row.ZaaerId.HasValue && row.ZaaerId.Value > 0)
                {
                    rateKeys.Add(row.ZaaerId.Value);
                }
            }

            return (rateKeys, internalRoomTypeId);
        }

        public static HashSet<int> BuildRateKeys(RoomType roomType)
        {
            return RoomTypeRateResolver.BuildRateLookupKeys(roomType);
        }
    }
}
