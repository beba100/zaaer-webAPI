#pragma warning disable CS1591

using FinanceLedgerAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.Security;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Controllers.Pms
{
    /// <summary>
    /// PMS apartment helpers (picker, etc.).
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/apartments")]
    [Produces("application/json")]
    public sealed class PmsApartmentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PmsApartmentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Apartments for the hotel with room type / building / floor labels for multi-unit reservation picker.
        /// </summary>
        [HttpGet("for-picker")]
        [RequireAnyPermission("reservations.unit_add", "reservations.unit_change")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetForPicker([FromQuery] int hotelId, CancellationToken cancellationToken)
        {
            var apartmentRows = await (
                from a in _context.Apartments.AsNoTracking()
                where a.HotelId == hotelId
                from rt in _context.RoomTypes.AsNoTracking()
                    .Where(rt =>
                        rt.HotelId == a.HotelId &&
                        a.RoomTypeId != null &&
                        (a.RoomTypeId == rt.RoomTypeId || a.RoomTypeId == rt.ZaaerId))
                    .OrderBy(rt => a.RoomTypeId == rt.RoomTypeId ? 0 : 1)
                    .ThenBy(rt => rt.RoomTypeId)
                    .Take(1)
                    .DefaultIfEmpty()
                from b in _context.Buildings.AsNoTracking()
                    .Where(b =>
                        b.HotelId == a.HotelId &&
                        a.BuildingId != null &&
                        (a.BuildingId == b.BuildingId || a.BuildingId == b.ZaaerId))
                    .OrderBy(b => a.BuildingId == b.BuildingId ? 0 : 1)
                    .ThenBy(b => b.BuildingId)
                    .Take(1)
                    .DefaultIfEmpty()
                from f in _context.Floors.AsNoTracking()
                    .Where(f =>
                        f.HotelId == a.HotelId &&
                        a.FloorId != null &&
                        (a.FloorId == f.FloorId || a.FloorId == f.ZaaerId))
                    .OrderBy(f => a.FloorId == f.FloorId ? 0 : 1)
                    .ThenBy(f => f.FloorId)
                    .Take(1)
                    .DefaultIfEmpty()
                orderby b != null ? b.BuildingName : "",
                    f != null ? f.FloorName : "",
                    a.ApartmentCode
                select new ApartmentPickerQueryRow
                {
                    ApartmentId = a.ApartmentId,
                    ZaaerId = a.ZaaerId,
                    ApartmentCode = a.ApartmentCode,
                    ApartmentName = a.ApartmentName,
                    Status = a.Status,
                    HousekeepingStatus = a.HousekeepingStatus,
                    RoomTypeId = a.RoomTypeId,
                    RoomTypeName = rt != null ? rt.RoomTypeName : null,
                    BuildingName = b != null ? b.BuildingName : null,
                    FloorName = f != null ? f.FloorName : null,
                    ResolvedRoomTypeId = rt != null ? rt.RoomTypeId : null,
                    ResolvedRoomTypeZaaerId = rt != null ? rt.ZaaerId : null
                }).ToListAsync(cancellationToken);

            var rates = await _context.RoomTypeRates.AsNoTracking()
                .Where(r => r.HotelId == hotelId)
                .ToListAsync(cancellationToken);

            var today = KsaTime.Now.Date;
            var tomorrow = today.AddDays(1);
            var activeMaintenanceUnitIds = (await _context.Maintenances.AsNoTracking()
                    .Where(m =>
                        m.HotelId == hotelId &&
                        m.FromDate < tomorrow &&
                        m.ToDate >= today)
                    .Select(m => new { m.UnitId, m.Status })
                    .ToListAsync(cancellationToken))
                .Where(m => IsActiveMaintenanceRecordStatus(m.Status))
                .Select(m => m.UnitId)
                .ToHashSet();

            var rows = apartmentRows.Select(row =>
            {
                var rate = PickSuggestedRate(row, rates);
                return new
                {
                    apartmentId = row.ApartmentId,
                    zaaerId = row.ZaaerId,
                    apartmentCode = row.ApartmentCode,
                    apartmentName = row.ApartmentName,
                    status = row.Status,
                    housekeepingStatus = row.HousekeepingStatus,
                    maintenanceActive = ApartmentHasActiveMaintenance(row, activeMaintenanceUnitIds),
                    roomTypeId = row.RoomTypeId,
                    roomTypeName = row.RoomTypeName,
                    buildingName = row.BuildingName,
                    floorName = row.FloorName,
                    dailySuggestedGross = rate == null
                        ? (decimal?)null
                        : rate.DailyRateMin ?? rate.DailyRateLowWeekdays ?? rate.DailyRateHighWeekdays,
                    monthlySuggestedGross = rate == null
                        ? (decimal?)null
                        : rate.MonthlyRateMin ?? rate.MonthlyRate
                };
            }).ToList();

            return Ok(new { success = true, data = rows });
        }

        private sealed class ApartmentPickerQueryRow
        {
            public int ApartmentId { get; init; }
            public int? ZaaerId { get; init; }
            public string? ApartmentCode { get; init; }
            public string? ApartmentName { get; init; }
            public string? Status { get; init; }
            public string? HousekeepingStatus { get; init; }
            public int? RoomTypeId { get; init; }
            public string? RoomTypeName { get; init; }
            public string? BuildingName { get; init; }
            public string? FloorName { get; init; }
            public int? ResolvedRoomTypeId { get; init; }
            public int? ResolvedRoomTypeZaaerId { get; init; }
        }

        /// <summary>
        /// <c>maintenances.unit_id</c> stores Zaaer id when present, otherwise internal apartment id.
        /// </summary>
        private static bool ApartmentHasActiveMaintenance(
            ApartmentPickerQueryRow row,
            IReadOnlySet<int> activeMaintenanceUnitIds)
        {
            if (activeMaintenanceUnitIds.Count == 0)
            {
                return false;
            }

            var boardUnitId = row.ZaaerId ?? row.ApartmentId;
            if (activeMaintenanceUnitIds.Contains(boardUnitId))
            {
                return true;
            }

            if (activeMaintenanceUnitIds.Contains(row.ApartmentId))
            {
                return true;
            }

            return row.ZaaerId is > 0 && activeMaintenanceUnitIds.Contains(row.ZaaerId.Value);
        }

        private static bool IsActiveMaintenanceRecordStatus(string? status)
        {
            var normalized = (status ?? string.Empty).Trim().ToLowerInvariant().Replace("_", "-");
            return normalized is "" or "active" or "maintenance" or "open" or "inprogress" or "in-progress";
        }

        private static RoomTypeRate? PickSuggestedRate(ApartmentPickerQueryRow row, IReadOnlyList<RoomTypeRate> rates)
        {
            if (rates.Count == 0)
            {
                return null;
            }

            var keys = new HashSet<int>();
            if (row.RoomTypeId is > 0)
            {
                keys.Add(row.RoomTypeId.Value);
            }

            if (row.ResolvedRoomTypeId is > 0)
            {
                keys.Add(row.ResolvedRoomTypeId.Value);
            }

            if (row.ResolvedRoomTypeZaaerId is > 0)
            {
                keys.Add(row.ResolvedRoomTypeZaaerId.Value);
            }

            if (keys.Count == 0)
            {
                return null;
            }

            return rates
                .Where(r => keys.Contains(r.RoomTypeId) || (r.ZaaerId.HasValue && keys.Contains(r.ZaaerId.Value)))
                .OrderBy(r =>
                    row.ResolvedRoomTypeId.HasValue && r.RoomTypeId == row.ResolvedRoomTypeId
                        ? 0
                        : row.ResolvedRoomTypeZaaerId.HasValue && r.RoomTypeId == row.ResolvedRoomTypeZaaerId
                            ? 1
                            : row.RoomTypeId.HasValue && r.RoomTypeId == row.RoomTypeId
                                ? 2
                                : 3)
                .ThenBy(r => r.RateId)
                .FirstOrDefault();
        }
    }
}
