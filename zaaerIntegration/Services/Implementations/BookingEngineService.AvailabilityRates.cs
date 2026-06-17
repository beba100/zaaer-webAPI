#pragma warning disable CS1591

using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.BookingEngine;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    public sealed partial class BookingEngineService
    {
        public async Task<IReadOnlyList<BookingEngineAvailabilityOverrideDto>> ListAvailabilityOverridesAsync(
            int hotelId,
            string? fromDate,
            string? toDate,
            CancellationToken cancellationToken = default)
        {
            await using var ctx = OpenAdminContext();
            var hotel = await ctx.HotelSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Hotel settings not found.");
            var hotelScope = HotelScope.From(hotel);

            var from = ParseDateOnlyOrToday(fromDate);
            var to = ParseDateOnlyOrToday(toDate);
            if (to < from)
            {
                (from, to) = (to, from);
            }

            if ((to - from).Days > 90)
            {
                to = from.AddDays(90);
            }

            var rows = await ctx.BookingEngineAvailabilityOverrides.AsNoTracking()
                .Where(o =>
                    (o.HotelId == hotelScope.ScopeHotelId || o.HotelId == hotelScope.LocalHotelId) &&
                    o.RateDate >= from &&
                    o.RateDate <= to)
                .OrderBy(o => o.RateDate)
                .ThenBy(o => o.RoomTypeId)
                .ToListAsync(cancellationToken);

            var roomTypes = await ctx.RoomTypes.AsNoTracking()
                .Where(rt => rt.HotelId == hotelScope.ScopeHotelId || rt.HotelId == hotelScope.LocalHotelId)
                .Select(rt => new { rt.RoomTypeId, rt.ZaaerId, Name = rt.RoomTypeName ?? rt.RoomTypeNameEn })
                .ToListAsync(cancellationToken);

            string? NameFor(int roomTypeRef)
            {
                var rt = roomTypes.FirstOrDefault(r =>
                    r.ZaaerId == roomTypeRef || r.RoomTypeId == roomTypeRef);
                return rt?.Name;
            }

            return rows.Select(o => new BookingEngineAvailabilityOverrideDto
            {
                OverrideId = o.OverrideId,
                RoomTypeId = o.RoomTypeId,
                RoomTypeName = NameFor(o.RoomTypeId),
                RateDate = o.RateDate.ToString("yyyy-MM-dd"),
                DisplayUnits = o.DisplayUnits
            }).ToList();
        }

        public async Task<IReadOnlyList<BookingEngineAvailabilityOverrideDto>> SaveAvailabilityOverridesAsync(
            int hotelId,
            BookingEngineAvailabilityOverrideBatchDto batch,
            CancellationToken cancellationToken = default)
        {
            await using var ctx = OpenAdminContext();
            var hotel = await ctx.HotelSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Hotel settings not found.");
            var hotelScope = HotelScope.From(hotel);
            var storageHotelId = hotelScope.ScopeHotelId;

            foreach (var item in batch.Items ?? new List<BookingEngineAvailabilityOverrideUpsertDto>())
            {
                if (!DateOnly.TryParse(item.DateFrom, out var fromDo) ||
                    !DateOnly.TryParse(item.DateTo, out var toDo))
                {
                    throw new ArgumentException("Invalid date range on availability override.");
                }

                var from = fromDo.ToDateTime(TimeOnly.MinValue);
                var to = toDo.ToDateTime(TimeOnly.MinValue);
                if (to < from)
                {
                    (from, to) = (to, from);
                }

                var roomType = await ctx.RoomTypes.AsNoTracking()
                    .FirstOrDefaultAsync(
                        rt => (rt.HotelId == hotelScope.ScopeHotelId || rt.HotelId == hotelScope.LocalHotelId) &&
                              (rt.RoomTypeId == item.RoomTypeId || rt.ZaaerId == item.RoomTypeId),
                        cancellationToken)
                    ?? throw new InvalidOperationException($"Room type {item.RoomTypeId} not found.");

                var storageRoomTypeId = RoomTypeGrossRateResolver.ResolveStorageRoomTypeId(roomType);
                var matchIds = new HashSet<int> { storageRoomTypeId, roomType.RoomTypeId };
                if (roomType.ZaaerId is > 0)
                {
                    matchIds.Add(roomType.ZaaerId.Value);
                }

                var existing = await ctx.BookingEngineAvailabilityOverrides
                    .Where(o =>
                        (o.HotelId == hotelScope.ScopeHotelId || o.HotelId == hotelScope.LocalHotelId) &&
                        matchIds.Contains(o.RoomTypeId) &&
                        o.RateDate >= from &&
                        o.RateDate <= to)
                    .ToListAsync(cancellationToken);

                if (item.DisplayUnits < 0)
                {
                    throw new ArgumentException("Display units cannot be negative.");
                }

                for (var d = from; d <= to; d = d.AddDays(1))
                {
                    var row = existing.FirstOrDefault(e => e.RateDate.Date == d.Date);
                    if (row == null)
                    {
                        ctx.BookingEngineAvailabilityOverrides.Add(new BookingEngineAvailabilityOverride
                        {
                            HotelId = storageHotelId,
                            RoomTypeId = storageRoomTypeId,
                            RateDate = d,
                            DisplayUnits = item.DisplayUnits,
                            CreatedAt = KsaTime.Now
                        });
                    }
                    else
                    {
                        row.HotelId = storageHotelId;
                        row.RoomTypeId = storageRoomTypeId;
                        row.DisplayUnits = item.DisplayUnits;
                        row.UpdatedAt = KsaTime.Now;
                    }
                }
            }

            await ctx.SaveChangesAsync(cancellationToken);

            var minDate = batch.Items?.Min(i => i.DateFrom) ?? KsaTime.Now.ToString("yyyy-MM-dd");
            var maxDate = batch.Items?.Max(i => i.DateTo) ?? minDate;
            return await ListAvailabilityOverridesAsync(hotelId, minDate, maxDate, cancellationToken);
        }

        private static DateTime ParseDateOnlyOrToday(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value) && DateOnly.TryParse(value.Trim(), out var d))
            {
                return d.ToDateTime(TimeOnly.MinValue);
            }

            return KsaTime.Now.Date;
        }

        private static int? FindAvailabilityOverride(
            IReadOnlyList<BookingEngineAvailabilityOverride> rows,
            HashSet<int> roomTypeKeys)
        {
            var hit = rows.FirstOrDefault(r => roomTypeKeys.Contains(r.RoomTypeId));
            return hit?.DisplayUnits;
        }

        private static string NormalizeAvailabilityMode(string? mode)
        {
            var x = (mode ?? BookingEngineAvailabilityModes.Actual).Trim().ToLowerInvariant();
            return x is BookingEngineAvailabilityModes.Override or BookingEngineAvailabilityModes.MinActualOverride
                ? x
                : BookingEngineAvailabilityModes.Actual;
        }

        private static string NormalizeRateFallbackMode(string? mode)
        {
            var x = (mode ?? "standard").Trim().ToLowerInvariant();
            return x == "programmatic" ? "programmatic" : "standard";
        }
    }
}
