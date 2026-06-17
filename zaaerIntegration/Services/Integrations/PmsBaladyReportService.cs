using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Integrations;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Services.Integrations
{
    public sealed class PmsBaladyReportService : PmsHotelScopeService, IPmsBaladyReportService
    {
        private readonly ApplicationDbContext _context;

        public PmsBaladyReportService(
            ApplicationDbContext context,
            ITenantService tenantService)
            : base(context, tenantService)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<BaladyReportRowDto>> GetReportAsync(
            BaladyReportQueryDto query,
            CancellationToken cancellationToken = default)
        {
            var hotel = await GetCurrentHotelSettingsAsync(cancellationToken);
            var hotelZaaerId = hotel.ZaaerId ?? hotel.HotelId;
            var hotelIds = new HashSet<int> { hotel.HotelId, hotelZaaerId };

            var monthStart = new DateTime(query.Year, query.Month, 1);
            var monthEndExclusive = monthStart.AddMonths(1);

            var reservations = await _context.Reservations.AsNoTracking()
                .Where(r => hotelIds.Contains(r.HotelId)
                    && r.CheckInDate != null
                    && r.CheckInDate < monthEndExclusive
                    && (r.DepartureDate ?? r.CheckOutDate ?? r.CheckInDate) > monthStart
                    && (r.Status == null
                        || (r.Status != "cancelled"
                            && r.Status != "canceled"
                            && r.Status != "Cancelled"
                            && r.Status != "Canceled")))
                .Select(r => new ReservationSlice
                {
                    ReservationId = r.ReservationId,
                    ZaaerId = r.ZaaerId,
                    ReservationNo = r.ReservationNo,
                    CustomerId = r.CustomerId,
                    CheckInDate = r.CheckInDate!.Value,
                    CheckOutDate = r.CheckOutDate,
                    DepartureDate = r.DepartureDate ?? r.CheckOutDate,
                    RentalType = r.RentalType,
                    MonthlyCalendarMode = r.MonthlyCalendarMode,
                    NumberOfMonths = r.NumberOfMonths,
                    Subtotal = r.Subtotal,
                    TotalAmount = r.TotalAmount,
                    TotalTaxAmount = r.TotalTaxAmount
                })
                .ToListAsync(cancellationToken);

            if (reservations.Count == 0)
            {
                return Array.Empty<BaladyReportRowDto>();
            }

            var unitReservationKeys = reservations
                .SelectMany(r => new[] { r.ReservationId, r.ZaaerId ?? -1 })
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var unitRowsRaw = await (
                    from u in _context.ReservationUnits.AsNoTracking()
                    where unitReservationKeys.Contains(u.ReservationId)
                    orderby u.CheckInDate, u.UnitId
                    select new
                    {
                        u.ReservationId,
                        u.ApartmentId,
                        u.CheckInDate,
                        u.UnitId,
                        u.RentAmount
                    })
                .ToListAsync(cancellationToken);

            var rentSumByReservationId = unitRowsRaw
                .GroupBy(u => u.ReservationId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.RentAmount));

            var apartmentIds = unitRowsRaw.Select(u => u.ApartmentId).Distinct().ToList();
            var apartments = await _context.Apartments.AsNoTracking()
                .Where(a => apartmentIds.Contains(a.ApartmentId)
                    || (a.ZaaerId.HasValue && apartmentIds.Contains(a.ZaaerId.Value)))
                .Select(a => new ApartmentSlice
                {
                    ApartmentId = a.ApartmentId,
                    ZaaerId = a.ZaaerId,
                    ApartmentName = a.ApartmentName,
                    ApartmentCode = a.ApartmentCode,
                    RoomTypeId = a.RoomTypeId
                })
                .ToListAsync(cancellationToken);

            var roomTypeIds = apartments
                .Where(a => a.RoomTypeId.HasValue)
                .Select(a => a.RoomTypeId!.Value)
                .Distinct()
                .ToList();

            var roomTypes = await _context.RoomTypes.AsNoTracking()
                .Where(rt => roomTypeIds.Contains(rt.RoomTypeId)
                    || (rt.ZaaerId.HasValue && roomTypeIds.Contains(rt.ZaaerId.Value)))
                .Select(rt => new RoomTypeSlice
                {
                    RoomTypeId = rt.RoomTypeId,
                    ZaaerId = rt.ZaaerId,
                    RoomTypeName = rt.RoomTypeName
                })
                .ToListAsync(cancellationToken);

            var apartmentById = apartments
                .SelectMany(a => new[]
                {
                    new { Id = a.ApartmentId, Apartment = a },
                    a.ZaaerId.HasValue ? new { Id = a.ZaaerId.Value, Apartment = a } : null
                })
                .Where(x => x != null)
                .GroupBy(x => x!.Id)
                .ToDictionary(g => g.Key, g => g.First()!.Apartment);

            var roomTypeById = roomTypes
                .SelectMany(rt => new[]
                {
                    new { Id = rt.RoomTypeId, RoomType = rt },
                    rt.ZaaerId.HasValue ? new { Id = rt.ZaaerId.Value, RoomType = rt } : null
                })
                .Where(x => x != null)
                .GroupBy(x => x!.Id)
                .ToDictionary(g => g.Key, g => g.First()!.RoomType);

            var firstUnitByReservationId = unitRowsRaw
                .GroupBy(u => u.ReservationId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var row = g.OrderBy(x => x.CheckInDate).ThenBy(x => x.UnitId).First();
                        apartmentById.TryGetValue(row.ApartmentId, out var apartment);
                        RoomTypeSlice? roomType = null;
                        if (apartment?.RoomTypeId != null
                            && roomTypeById.TryGetValue(apartment.RoomTypeId.Value, out var rt))
                        {
                            roomType = rt;
                        }

                        return new UnitSlice
                        {
                            ReservationId = row.ReservationId,
                            ApartmentName = apartment?.ApartmentName,
                            ApartmentCode = apartment?.ApartmentCode ?? string.Empty,
                            RoomTypeName = roomType?.RoomTypeName
                        };
                    });

            var customerRefs = reservations
                .Select(r => r.CustomerId)
                .Where(id => id is > 0)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
            var customers = await _context.Customers.AsNoTracking()
                .Where(c => customerRefs.Contains(c.CustomerId)
                    || (c.ZaaerId.HasValue && customerRefs.Contains(c.ZaaerId.Value)))
                .Select(c => new CustomerSlice
                {
                    CustomerId = c.CustomerId,
                    ZaaerId = c.ZaaerId,
                    CustomerName = c.CustomerName
                })
                .ToListAsync(cancellationToken);

            var rows = new List<BaladyReportRowDto>(reservations.Count);
            foreach (var reservation in reservations)
            {
                var bounds = BaladyStayAmountCalculator.ResolveStayBounds(
                    reservation.CheckInDate,
                    reservation.CheckOutDate,
                    reservation.DepartureDate);

                var daysInMonth = BaladyStayAmountCalculator.CountDaysInMonth(
                    bounds,
                    monthStart,
                    monthEndExclusive);

                if (daysInMonth <= 0)
                {
                    continue;
                }

                var periodInMonth = BaladyStayAmountCalculator.ResolvePeriodInMonth(
                    bounds,
                    monthStart,
                    monthEndExclusive);

                if (!periodInMonth.PeriodFrom.HasValue || !periodInMonth.PeriodTo.HasValue)
                {
                    continue;
                }

                rentSumByReservationId.TryGetValue(reservation.ReservationId, out var unitRentSum);
                if (unitRentSum <= 0m
                    && reservation.ZaaerId.HasValue
                    && rentSumByReservationId.TryGetValue(reservation.ZaaerId.Value, out var byZaaerRent))
                {
                    unitRentSum = byZaaerRent;
                }

                var netBase = BaladyStayAmountCalculator.ResolveNetStayBase(
                    reservation.Subtotal,
                    reservation.TotalAmount,
                    reservation.TotalTaxAmount,
                    unitRentSum);

                var totalStayDays = BaladyStayAmountCalculator.CountTotalStayDays(
                    bounds,
                    reservation.RentalType,
                    reservation.MonthlyCalendarMode,
                    reservation.NumberOfMonths);

                var amount = BaladyStayAmountCalculator.CalculateAmount(netBase, totalStayDays, daysInMonth);
                if (amount <= 0m)
                {
                    continue;
                }

                var unit = ResolveFirstUnit(reservation, firstUnitByReservationId);

                rows.Add(new BaladyReportRowDto
                {
                    RoomNumber = ResolveRoomNumber(unit),
                    PeriodFrom = periodInMonth.PeriodFrom,
                    PeriodTo = periodInMonth.PeriodTo,
                    Amount = amount,
                    CustomerName = reservation.CustomerId is > 0
                        ? ResolveCustomerName(reservation.CustomerId.Value, customers)
                        : null,
                    BookingNumber = !string.IsNullOrWhiteSpace(reservation.ReservationNo)
                        ? reservation.ReservationNo
                        : reservation.ZaaerId?.ToString() ?? reservation.ReservationId.ToString(),
                    RoomType = unit?.RoomTypeName,
                    Notes = null
                });
            }

            return rows
                .OrderBy(r => r.BookingNumber)
                .ThenBy(r => r.PeriodFrom)
                .ToList();
        }

        private static UnitSlice? ResolveFirstUnit(
            ReservationSlice reservation,
            IReadOnlyDictionary<int, UnitSlice> firstUnitByReservationId)
        {
            if (firstUnitByReservationId.TryGetValue(reservation.ReservationId, out var byPk))
            {
                return byPk;
            }

            if (reservation.ZaaerId.HasValue
                && firstUnitByReservationId.TryGetValue(reservation.ZaaerId.Value, out var byZaaer))
            {
                return byZaaer;
            }

            return null;
        }

        private static string? ResolveRoomNumber(UnitSlice? unit)
        {
            if (unit == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(unit.ApartmentName))
            {
                return unit.ApartmentName.Trim();
            }

            return string.IsNullOrWhiteSpace(unit.ApartmentCode) ? null : unit.ApartmentCode.Trim();
        }

        private static string? ResolveCustomerName(int customerRef, IReadOnlyList<CustomerSlice> customers)
        {
            var customer = customers.FirstOrDefault(c =>
                c.CustomerId == customerRef
                || (c.ZaaerId.HasValue && c.ZaaerId.Value == customerRef));

            return string.IsNullOrWhiteSpace(customer?.CustomerName) ? null : customer.CustomerName;
        }

        private sealed class ReservationSlice
        {
            public int ReservationId { get; init; }

            public int? ZaaerId { get; init; }

            public string ReservationNo { get; init; } = string.Empty;

            public int? CustomerId { get; init; }

            public DateTime CheckInDate { get; init; }

            public DateTime? CheckOutDate { get; init; }

            public DateTime? DepartureDate { get; init; }

            public string? RentalType { get; init; }

            public string? MonthlyCalendarMode { get; init; }

            public int? NumberOfMonths { get; init; }

            public decimal? Subtotal { get; init; }

            public decimal? TotalAmount { get; init; }

            public decimal? TotalTaxAmount { get; init; }
        }

        private sealed class ApartmentSlice
        {
            public int ApartmentId { get; init; }

            public int? ZaaerId { get; init; }

            public string? ApartmentName { get; init; }

            public string ApartmentCode { get; init; } = string.Empty;

            public int? RoomTypeId { get; init; }
        }

        private sealed class RoomTypeSlice
        {
            public int RoomTypeId { get; init; }

            public int? ZaaerId { get; init; }

            public string RoomTypeName { get; init; } = string.Empty;
        }

        private sealed class UnitSlice
        {
            public int ReservationId { get; init; }

            public string? ApartmentName { get; init; }

            public string ApartmentCode { get; init; } = string.Empty;

            public string? RoomTypeName { get; init; }
        }

        private sealed class CustomerSlice
        {
            public int CustomerId { get; init; }

            public int? ZaaerId { get; init; }

            public string CustomerName { get; init; } = string.Empty;
        }
    }
}
