using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms.Property;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    public sealed class PmsRoomTypeRatesService : IPmsRoomTypeRatesService
    {
        private const int DefaultCalendarDays = 14;

        private readonly ApplicationDbContext _context;
        private readonly ITenantService _tenantService;
        private readonly IPmsPropertyService _propertyService;

        public PmsRoomTypeRatesService(
            ApplicationDbContext context,
            ITenantService tenantService,
            IPmsPropertyService propertyService)
        {
            _context = context;
            _tenantService = tenantService;
            _propertyService = propertyService;
        }

        public Task<int> ResolveCurrentHotelIdAsync(CancellationToken cancellationToken = default) =>
            _propertyService.ResolveCurrentHotelIdAsync(cancellationToken);

        public async Task<IReadOnlyList<PmsRoomTypeRateListItemDto>> ListRoomTypeRatesAsync(
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var roomTypes = await _context.RoomTypes.AsNoTracking()
                .Where(rt =>
                    rt.IsActive &&
                    (rt.HotelId == scope.ScopeHotelId || rt.HotelId == scope.LocalHotelId))
                .OrderBy(rt => rt.SortOrder)
                .ThenBy(rt => rt.RoomTypeName)
                .ToListAsync(cancellationToken);

            var rates = await _context.RoomTypeRates.AsNoTracking()
                .Where(r => r.HotelId == scope.ScopeHotelId || r.HotelId == scope.LocalHotelId)
                .ToListAsync(cancellationToken);

            var result = new List<PmsRoomTypeRateListItemDto>();
            foreach (var rt in roomTypes)
            {
                var keys = RoomTypeRateResolver.BuildRateLookupKeys(rt);
                var rate = rates.FirstOrDefault(r => RoomTypeRateResolver.RateMatchesRoomType(r, keys));
                result.Add(new PmsRoomTypeRateListItemDto
                {
                    RateId = rate?.RateId ?? 0,
                    RoomTypeId = rt.RoomTypeId,
                    RoomTypeZaaerId = rt.ZaaerId,
                    RoomTypeName = rt.RoomTypeName,
                    RoomTypeNameEn = rt.RoomTypeNameEn,
                    DailyRateLowWeekdays = rate?.DailyRateLowWeekdays,
                    DailyRateHighWeekdays = rate?.DailyRateHighWeekdays,
                    DailyRateMin = rate?.DailyRateMin,
                    MonthlyRate = rate?.MonthlyRate,
                    MonthlyRateMin = rate?.MonthlyRateMin,
                    OtaRateLowWeekdays = rate?.OtaRateLowWeekdays,
                    OtaRateHighWeekdays = rate?.OtaRateHighWeekdays
                });
            }

            return result;
        }

        public async Task<PmsRoomTypeRateListItemDto?> UpdateRoomTypeRateAsync(
            int rateId,
            PmsUpdateRoomTypeRateDto dto,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var roomType = await ResolveRoomTypeAsync(dto.RoomTypeId, scope, cancellationToken, tracking: false);
            if (roomType == null)
            {
                return null;
            }

            RoomTypeRate? entity;
            if (rateId > 0)
            {
                entity = await _context.RoomTypeRates.FirstOrDefaultAsync(
                    r => r.RateId == rateId && (r.HotelId == scope.ScopeHotelId || r.HotelId == scope.LocalHotelId),
                    cancellationToken);
            }
            else
            {
                entity = null;
            }

            if (entity == null)
            {
                entity = new RoomTypeRate
                {
                    HotelId = scope.ScopeHotelId,
                    RoomTypeId = roomType.RoomTypeId,
                    CreatedAt = KsaTime.Now
                };
                _context.RoomTypeRates.Add(entity);
            }

            entity.DailyRateLowWeekdays = dto.DailyRateLowWeekdays;
            entity.DailyRateHighWeekdays = dto.DailyRateHighWeekdays;
            entity.DailyRateMin = dto.DailyRateMin;
            entity.MonthlyRate = dto.MonthlyRate;
            entity.MonthlyRateMin = dto.MonthlyRateMin;
            entity.OtaRateLowWeekdays = dto.OtaRateLowWeekdays;
            entity.OtaRateHighWeekdays = dto.OtaRateHighWeekdays;
            entity.UpdatedAt = KsaTime.Now;

            await _context.SaveChangesAsync(cancellationToken);

            return new PmsRoomTypeRateListItemDto
            {
                RateId = entity.RateId,
                RoomTypeId = roomType.RoomTypeId,
                RoomTypeZaaerId = roomType.ZaaerId,
                RoomTypeName = roomType.RoomTypeName,
                RoomTypeNameEn = roomType.RoomTypeNameEn,
                DailyRateLowWeekdays = entity.DailyRateLowWeekdays,
                DailyRateHighWeekdays = entity.DailyRateHighWeekdays,
                DailyRateMin = entity.DailyRateMin,
                MonthlyRate = entity.MonthlyRate,
                MonthlyRateMin = entity.MonthlyRateMin,
                OtaRateLowWeekdays = entity.OtaRateLowWeekdays,
                OtaRateHighWeekdays = entity.OtaRateHighWeekdays
            };
        }

        public async Task<PmsRatesCalendarDto> GetRatesCalendarAsync(
            string? fromDate,
            string? toDate,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var (from, to) = ParseDateRange(fromDate, toDate);

            var roomTypes = await _context.RoomTypes.AsNoTracking()
                .Where(rt =>
                    rt.IsActive &&
                    (rt.HotelId == scope.ScopeHotelId || rt.HotelId == scope.LocalHotelId))
                .OrderBy(rt => rt.SortOrder)
                .ThenBy(rt => rt.RoomTypeName)
                .ToListAsync(cancellationToken);

            var baseRates = await _context.RoomTypeRates.AsNoTracking()
                .Where(r => r.HotelId == scope.ScopeHotelId || r.HotelId == scope.LocalHotelId)
                .ToListAsync(cancellationToken);

            var dailyRows = await _context.RoomTypeDailyRates.AsNoTracking()
                .Where(d =>
                    (d.HotelId == scope.ScopeHotelId || d.HotelId == scope.LocalHotelId) &&
                    d.RateDate >= from &&
                    d.RateDate <= to)
                .ToListAsync(cancellationToken);

            var apartments = await _context.Apartments.AsNoTracking()
                .Where(a =>
                    (a.IsActive == null || a.IsActive == true) &&
                    a.RoomTypeId.HasValue &&
                    (a.HotelId == scope.ScopeHotelId || a.HotelId == scope.LocalHotelId))
                .ToListAsync(cancellationToken);

            var availabilityContext = await BuildAvailabilityContextAsync(
                scope,
                apartments,
                from,
                to.AddDays(1),
                cancellationToken);

            var days = BuildDayHeaders(from, to);
            var rows = new List<PmsRatesCalendarRowDto>();

            foreach (var rt in roomTypes)
            {
                var keys = RoomTypeRateResolver.BuildRateLookupKeys(rt);
                var linkId = PropertyEntityLinks.GetRoomTypeLinkId(rt);
                var typeApartments = apartments.Where(a =>
                    a.RoomTypeId == rt.RoomTypeId ||
                    (linkId.HasValue && a.RoomTypeId == linkId.Value)).ToList();

                var baseRate = baseRates.FirstOrDefault(r => RoomTypeRateResolver.RateMatchesRoomType(r, keys));
                var overrides = dailyRows
                    .Where(d => keys.Contains(d.RoomTypeId) && d.GrossRate > 0m)
                    .ToDictionary(d => d.RateDate.Date, d => d.GrossRate);

                var availCells = new List<PmsRatesCalendarCellDto>();
                var priceCells = new List<PmsRatesCalendarCellDto>();

                foreach (var day in days)
                {
                    var date = DateTime.Parse(day.Date);
                    var total = typeApartments.Count;
                    var available = CountAvailableForNight(
                        availabilityContext,
                        typeApartments,
                        date);

                    var price = RoomTypeRateResolver.ResolveDailyGross(baseRate, overrides, date);
                    var isOverride = overrides.ContainsKey(date.Date);

                    availCells.Add(new PmsRatesCalendarCellDto
                    {
                        Date = day.Date,
                        TotalUnits = total,
                        AvailableUnits = available,
                        Price = null,
                        IsOverride = false
                    });

                    priceCells.Add(new PmsRatesCalendarCellDto
                    {
                        Date = day.Date,
                        TotalUnits = null,
                        AvailableUnits = null,
                        Price = price > 0 ? price : null,
                        IsOverride = isOverride
                    });
                }

                rows.Add(new PmsRatesCalendarRowDto
                {
                    RoomTypeId = rt.RoomTypeId,
                    RoomTypeName = rt.RoomTypeName,
                    RowKind = "availability",
                    RowLabel = "availability",
                    Cells = availCells
                });

                rows.Add(new PmsRatesCalendarRowDto
                {
                    RoomTypeId = rt.RoomTypeId,
                    RoomTypeName = rt.RoomTypeName,
                    RowKind = "price",
                    RowLabel = "price",
                    Cells = priceCells
                });
            }

            return new PmsRatesCalendarDto
            {
                FromDate = FormatDate(from),
                ToDate = FormatDate(to),
                Days = days,
                Rows = rows
            };
        }

        public async Task UpsertDailyRatesAsync(PmsUpsertDailyRatesDto dto, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var roomType = await ResolveRoomTypeAsync(dto.RoomTypeId, scope, cancellationToken, tracking: false)
                ?? throw new InvalidOperationException("Room type not found.");

            if (!TryParseDateOnly(dto.DateFrom, out var from) || !TryParseDateOnly(dto.DateTo, out var to))
            {
                throw new ArgumentException("Invalid date range.");
            }

            if (to < from)
            {
                (from, to) = (to, from);
            }

            // room_type_daily_rates stores Zaaer scope ids (hotel_settings.zaaer_id, room_types.zaaer_id).
            var storageHotelId = ResolveDailyRateHotelId(scope);
            var storageRoomTypeId = ResolveDailyRateRoomTypeId(roomType);
            var roomTypeMatchIds = BuildDailyRateRoomTypeMatchIds(roomType);
            var existing = await _context.RoomTypeDailyRates
                .Where(d =>
                    (d.HotelId == scope.ScopeHotelId || d.HotelId == scope.LocalHotelId) &&
                    roomTypeMatchIds.Contains(d.RoomTypeId) &&
                    d.RateDate >= from &&
                    d.RateDate <= to)
                .ToListAsync(cancellationToken);

            if (dto.GrossRate == null)
            {
                if (existing.Count > 0)
                {
                    _context.RoomTypeDailyRates.RemoveRange(existing);
                }

                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            if (dto.GrossRate.Value < 0)
            {
                throw new ArgumentException("Gross rate cannot be negative.");
            }

            var byDate = IndexDailyRatesByDate(existing, storageHotelId, storageRoomTypeId);
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                if (byDate.TryGetValue(d, out var row))
                {
                    row.GrossRate = dto.GrossRate.Value;
                    row.HotelId = storageHotelId;
                    row.RoomTypeId = storageRoomTypeId;
                    row.UpdatedAt = KsaTime.Now;
                }
                else
                {
                    _context.RoomTypeDailyRates.Add(new RoomTypeDailyRate
                    {
                        HotelId = storageHotelId,
                        RoomTypeId = storageRoomTypeId,
                        RateDate = d,
                        GrossRate = dto.GrossRate.Value,
                        CreatedAt = KsaTime.Now
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private Dictionary<DateTime, RoomTypeDailyRate> IndexDailyRatesByDate(
            IReadOnlyList<RoomTypeDailyRate> existing,
            int storageHotelId,
            int storageRoomTypeId)
        {
            var byDate = new Dictionary<DateTime, RoomTypeDailyRate>();
            foreach (var group in existing.GroupBy(e => e.RateDate.Date))
            {
                var keeper = group
                    .OrderByDescending(r => r.HotelId == storageHotelId && r.RoomTypeId == storageRoomTypeId)
                    .ThenByDescending(r => r.HotelId == storageHotelId)
                    .ThenByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                    .First();

                byDate[group.Key] = keeper;

                foreach (var duplicate in group.Where(r => r.DailyRateId != keeper.DailyRateId))
                {
                    _context.RoomTypeDailyRates.Remove(duplicate);
                }

                keeper.HotelId = storageHotelId;
                keeper.RoomTypeId = storageRoomTypeId;
            }

            return byDate;
        }

        private sealed class AvailabilityContext
        {
            public Dictionary<int, List<ReservationNightBlock>> BlocksByApartmentId { get; init; } = new();
            public List<MaintenanceNightBlock> Maintenances { get; init; } = new();
        }

        private sealed record ReservationNightBlock(DateTime CheckIn, DateTime EndDate, string? Status);

        private sealed record MaintenanceNightBlock(int UnitId, DateTime FromDate, DateTime ToDate);

        private async Task<AvailabilityContext> BuildAvailabilityContextAsync(
            PropertyHotelScope scope,
            IReadOnlyList<Apartment> apartments,
            DateTime rangeFrom,
            DateTime rangeToExclusive,
            CancellationToken cancellationToken)
        {
            var aptIdSet = new HashSet<int>();
            foreach (var apt in apartments)
            {
                aptIdSet.Add(apt.ApartmentId);
                if (apt.ZaaerId.HasValue)
                {
                    aptIdSet.Add(apt.ZaaerId.Value);
                }
            }

            var unitRows = await _context.ReservationUnits.AsNoTracking()
                .Where(unit =>
                    aptIdSet.Contains(unit.ApartmentId) &&
                    unit.CheckInDate < rangeToExclusive)
                .Select(unit => new
                {
                    unit.ApartmentId,
                    unit.CheckInDate,
                    unit.DepartureDate,
                    unit.CheckOutDate,
                    unit.Status
                })
                .ToListAsync(cancellationToken);

            var blocksByApt = new Dictionary<int, List<ReservationNightBlock>>();
            foreach (var row in unitRows)
            {
                var end = row.DepartureDate ?? row.CheckOutDate;
                if (end.Date < rangeFrom)
                {
                    continue;
                }

                if (!blocksByApt.TryGetValue(row.ApartmentId, out var list))
                {
                    list = new List<ReservationNightBlock>();
                    blocksByApt[row.ApartmentId] = list;
                }

                list.Add(new ReservationNightBlock(row.CheckInDate, end, row.Status));
            }

            var maintRows = await _context.Maintenances.AsNoTracking()
                .Where(m =>
                    (m.HotelId == scope.ScopeHotelId || m.HotelId == scope.LocalHotelId) &&
                    m.FromDate < rangeToExclusive &&
                    m.ToDate >= rangeFrom &&
                    (m.Status == null || m.Status == "" || m.Status == "active" || m.Status == "maintenance" || m.Status == "open" || m.Status == "inprogress"))
                .Select(m => new { m.UnitId, m.FromDate, m.ToDate })
                .ToListAsync(cancellationToken);

            return new AvailabilityContext
            {
                BlocksByApartmentId = blocksByApt,
                Maintenances = maintRows
                    .Select(m => new MaintenanceNightBlock(m.UnitId, m.FromDate, m.ToDate))
                    .ToList()
            };
        }

        private static int CountAvailableForNight(
            AvailabilityContext ctx,
            IReadOnlyList<Apartment> apartments,
            DateTime nightDate)
        {
            var checkIn = nightDate.Date;
            var checkOutExclusive = checkIn.AddDays(1);
            var count = 0;

            foreach (var apt in apartments)
            {
                if (!IsVacantApartmentStatus(apt.Status))
                {
                    continue;
                }

                if (IsApartmentBookableForNight(ctx, apt, checkIn, checkOutExclusive))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsApartmentBookableForNight(
            AvailabilityContext ctx,
            Apartment apt,
            DateTime checkIn,
            DateTime checkOutExclusive)
        {
            var st = (apt.Status ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "").Replace("_", "");
            if (st is "outoforder" or "ooo" or "maintenance" or "blocked")
            {
                return false;
            }

            var boardId = apt.ZaaerId ?? apt.ApartmentId;
            var aptIds = new[] { boardId, apt.ApartmentId };

            if (ctx.Maintenances.Any(m =>
                    aptIds.Contains(m.UnitId) &&
                    m.FromDate < checkOutExclusive &&
                    m.ToDate >= checkIn))
            {
                return false;
            }

            foreach (var aptId in aptIds)
            {
                if (!ctx.BlocksByApartmentId.TryGetValue(aptId, out var blocks))
                {
                    continue;
                }

                var hasOverlap = blocks.Any(unit =>
                {
                    if (unit.EndDate.Date < checkIn)
                    {
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(unit.Status))
                    {
                        return false;
                    }

                    var norm = unit.Status.Trim().ToLowerInvariant().Replace("-", "").Replace("_", "");
                    return norm is not "cancelled" and not "canceled" and not "checkedout" and not "noshow";
                });

                if (hasOverlap)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsVacantApartmentStatus(string? status)
        {
            var st = (status ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "").Replace("_", "");
            return st is "vacant" or "available" or "free";
        }

        private static List<PmsRatesCalendarDayDto> BuildDayHeaders(DateTime from, DateTime to)
        {
            var days = new List<PmsRatesCalendarDayDto>();
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                days.Add(new PmsRatesCalendarDayDto
                {
                    Date = FormatDate(d),
                    DayLabel = d.ToString("ddd dd MMM"),
                    IsWeekend = RoomTypeRateResolver.IsKsaHighWeekday(d)
                });
            }

            return days;
        }

        private static (DateTime From, DateTime To) ParseDateRange(string? fromDate, string? toDate)
        {
            var today = KsaTime.Now.Date;
            var from = TryParseDateOnly(fromDate, out var f) ? f : today;
            var to = TryParseDateOnly(toDate, out var t) ? t : from.AddDays(DefaultCalendarDays - 1);

            if (to < from)
            {
                to = from.AddDays(DefaultCalendarDays - 1);
            }

            var span = (to - from).Days;
            if (span > 60)
            {
                to = from.AddDays(60);
            }

            return (from, to);
        }

        private static bool TryParseDateOnly(string? value, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (DateOnly.TryParse(value.Trim(), out var d))
            {
                date = d.ToDateTime(TimeOnly.MinValue);
                return true;
            }

            return DateTime.TryParse(value, out date);
        }

        private static string FormatDate(DateTime date) => date.ToString("yyyy-MM-dd");

        private static int ResolveDailyRateHotelId(PropertyHotelScope scope) => scope.ScopeHotelId;

        private static int ResolveDailyRateRoomTypeId(RoomType roomType) =>
            roomType.ZaaerId is > 0 ? roomType.ZaaerId.Value : roomType.RoomTypeId;

        private static HashSet<int> BuildDailyRateRoomTypeMatchIds(RoomType roomType)
        {
            var ids = new HashSet<int> { roomType.RoomTypeId, ResolveDailyRateRoomTypeId(roomType) };
            if (roomType.ZaaerId is > 0)
            {
                ids.Add(roomType.ZaaerId.Value);
            }

            return ids;
        }

        private sealed record PropertyHotelScope(int LocalHotelId, int ScopeHotelId);

        private async Task<PropertyHotelScope> GetCurrentHotelScopeAsync(CancellationToken cancellationToken)
        {
            var tenant = _tenantService.GetTenant()
                ?? throw new UnauthorizedAccessException("Tenant not resolved. Provide X-Hotel-Code.");

            var code = tenant.Code.Trim();
            var hotel = await _context.HotelSettings.AsNoTracking()
                .Where(h => h.HotelCode != null)
                .FirstOrDefaultAsync(
                    h => h.HotelCode!.ToLower() == code.ToLower(),
                    cancellationToken)
                ?? throw new InvalidOperationException($"HotelSettings not found for code: {tenant.Code}");

            if (!hotel.ZaaerId.HasValue)
            {
                return new PropertyHotelScope(hotel.HotelId, hotel.HotelId);
            }

            return new PropertyHotelScope(hotel.HotelId, hotel.ZaaerId.Value);
        }

        private async Task<RoomType?> ResolveRoomTypeAsync(
            int id,
            PropertyHotelScope scope,
            CancellationToken cancellationToken,
            bool tracking)
        {
            var query = tracking ? _context.RoomTypes : _context.RoomTypes.AsNoTracking();
            return await query.FirstOrDefaultAsync(
                rt => (rt.HotelId == scope.ScopeHotelId || rt.HotelId == scope.LocalHotelId) &&
                      (rt.RoomTypeId == id || rt.ZaaerId == id),
                cancellationToken);
        }
    }
}
