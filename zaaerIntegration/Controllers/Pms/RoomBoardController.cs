#pragma warning disable CS1591

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinanceLedgerAPI.Models;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.RoomBoard;
using zaaerIntegration.Security;
using zaaerIntegration.Services;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Controllers.Pms
{
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/room-board")]
    [Produces("application/json")]
    public sealed class RoomBoardController : ControllerBase
    {
        private readonly IRoomBoardService _roomBoardService;
        private readonly ApplicationDbContext _context;
        private readonly MasterDbContext _masterDbContext;
        private readonly ICurrentUserContext _currentUser;

        public RoomBoardController(
            IRoomBoardService roomBoardService,
            ApplicationDbContext context,
            MasterDbContext masterDbContext,
            ICurrentUserContext currentUser)
        {
            _roomBoardService = roomBoardService;
            _context = context;
            _masterDbContext = masterDbContext;
            _currentUser = currentUser;
        }

        /// <summary>
        /// Hotel codes from Master DB <c>Tenants</c> (<c>Code</c> aligns with tenant <c>hotel_settings.hotel_code</c>; <c>ZaaerId</c> with <c>hotel_id</c>).
        /// Only tenants with a non-empty <c>DatabaseName</c> (provisioned DB) are listed.
        /// Allowed without <c>X-Hotel-Code</c> so the room board can populate the filter on first load.
        /// </summary>
        [HttpGet("hotel-codes")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetHotelCodes(CancellationToken cancellationToken)
        {
            var query = TenantScope.FilterForUser(_masterDbContext.Tenants.AsNoTracking(), _currentUser);
            var rows = await query
                .Where(t =>
                    t.Code != null &&
                    t.Code.Trim() != "" &&
                    t.DatabaseName != null &&
                    t.DatabaseName.Trim() != "")
                .OrderBy(t => t.Id)
                .Select(t => new
                {
                    code = t.Code.Trim(),
                    name = t.Name,
                    nameEn = t.NameEn,
                    zaaerId = t.ZaaerId,
                    tenantId = t.Id
                })
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                success = true,
                data = rows
            });
        }

        [HttpGet]
        [RequirePermission("room_board.view")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetRoomBoard(
            [FromQuery] RoomBoardRequestDto request,
            CancellationToken cancellationToken)
        {
            var result = await _roomBoardService.GetRoomBoardAsync(request, cancellationToken);

            return Ok(new
            {
                success = true,
                message = "Room board loaded successfully.",
                data = result
            });
        }

        [HttpPut("apartments/{apartmentId:int}/card-color")]
        [RequirePermission("room_board.update_status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SaveRoomCardColor(
            [FromRoute] int apartmentId,
            [FromBody] RoomCardColorSettingRequestDto request,
            CancellationToken cancellationToken)
        {
            var apartment = await _context.Apartments
                .FirstOrDefaultAsync(a => a.ZaaerId == apartmentId || a.ApartmentId == apartmentId, cancellationToken);

            if (apartment == null)
            {
                return NotFound(new { success = false, message = "Apartment not found." });
            }

            var apartmentBoardId = apartment.ZaaerId ?? apartment.ApartmentId;
            var existing = await _context.RoomCardColorSettings
                .FirstOrDefaultAsync(x =>
                    x.HotelId == apartment.HotelId &&
                    x.ApartmentZaaerId == apartmentBoardId,
                    cancellationToken);

            static string? TrimColor(string? value)
            {
                var s = value == null ? null : value.Trim();
                return string.IsNullOrWhiteSpace(s) ? null : s;
            }

            var guest = TrimColor(request.OccupiedGuestBackColor);
            var text = TrimColor(request.OccupiedTextColor);

            if (guest == null && text == null)
            {
                if (existing != null)
                {
                    _context.RoomCardColorSettings.Remove(existing);
                    await _context.SaveChangesAsync(cancellationToken);
                }

                return Ok(new { success = true, data = (object?)null });
            }

            existing ??= new RoomCardColorSetting
            {
                HotelId = apartment.HotelId,
                ApartmentZaaerId = apartmentBoardId,
                CreatedAt = DateTime.Now
            };

            existing.OccupiedCardBackColor = null;
            existing.OccupiedHeaderBackColor = null;
            existing.OccupiedGuestBackColor = guest;
            existing.OccupiedDatesBackColor = null;
            existing.OccupiedTextColor = text;
            existing.IsActive = true;
            existing.UpdatedAt = DateTime.Now;

            if (existing.SettingId == 0)
            {
                _context.RoomCardColorSettings.Add(existing);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                success = true,
                data = new
                {
                    apartmentId = apartmentBoardId,
                    existing.OccupiedCardBackColor,
                    existing.OccupiedHeaderBackColor,
                    existing.OccupiedGuestBackColor,
                    existing.OccupiedDatesBackColor,
                    existing.OccupiedTextColor
                }
            });
        }

        [HttpDelete("apartments/{apartmentId:int}/card-color")]
        [RequirePermission("room_board.update_status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteRoomCardColor(
            [FromRoute] int apartmentId,
            CancellationToken cancellationToken)
        {
            var apartment = await _context.Apartments
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ZaaerId == apartmentId || a.ApartmentId == apartmentId, cancellationToken);

            if (apartment == null)
            {
                return Ok(new { success = true });
            }

            var apartmentBoardId = apartment.ZaaerId ?? apartment.ApartmentId;
            var existing = await _context.RoomCardColorSettings
                .FirstOrDefaultAsync(x =>
                    x.HotelId == apartment.HotelId &&
                    x.ApartmentZaaerId == apartmentBoardId,
                    cancellationToken);

            if (existing != null)
            {
                _context.RoomCardColorSettings.Remove(existing);
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Ok(new { success = true });
        }

        [HttpGet("apartments/{apartmentId:int}/maintenances")]
        [RequirePermission("room_board.view")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetApartmentMaintenances(
            [FromRoute] int apartmentId,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            var (found, rows) = await _roomBoardService.GetApartmentMaintenancesAsync(apartmentId, hotelId, cancellationToken);
            if (!found)
            {
                return NotFound(new { success = false, code = "NOT_FOUND" });
            }

            return Ok(new { success = true, data = new { rows } });
        }

        [HttpPost("apartments/{apartmentId:int}/maintenances")]
        [RequirePermission("room_board.update_status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateApartmentMaintenance(
            [FromRoute] int apartmentId,
            [FromBody] RoomBoardMaintenanceCreateRequestDto? body,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            if (body == null)
            {
                return BadRequest(new { success = false, code = "BODY_REQUIRED" });
            }

            var userId = PmsCurrentUser.ResolveUserId(_currentUser) ?? 0;
            var (ok, code, maintenanceId) = await _roomBoardService.CreateApartmentMaintenanceAsync(
                apartmentId,
                hotelId,
                body,
                userId,
                cancellationToken);

            if (!ok && string.Equals(code, "NOT_FOUND", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { success = false, code });
            }

            if (!ok)
            {
                return BadRequest(new { success = false, code });
            }

            return Ok(new { success = true, data = new { id = maintenanceId } });
        }

        [HttpPut("apartments/{apartmentId:int}/maintenances/{maintenanceId:int}")]
        [RequirePermission("room_board.update_status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateApartmentMaintenance(
            [FromRoute] int apartmentId,
            [FromRoute] int maintenanceId,
            [FromBody] RoomBoardMaintenanceUpdateRequestDto? body,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            if (body == null)
            {
                return BadRequest(new { success = false, code = "BODY_REQUIRED" });
            }

            var (ok, code) = await _roomBoardService.UpdateApartmentMaintenanceAsync(
                apartmentId,
                hotelId,
                maintenanceId,
                body,
                cancellationToken);

            if (!ok && string.Equals(code, "NOT_FOUND", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { success = false, code });
            }

            if (!ok)
            {
                return BadRequest(new { success = false, code });
            }

            return Ok(new { success = true });
        }

        [HttpDelete("apartments/{apartmentId:int}/maintenances/{maintenanceId:int}")]
        [RequirePermission("room_board.update_status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelApartmentMaintenance(
            [FromRoute] int apartmentId,
            [FromRoute] int maintenanceId,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            var (ok, code) = await _roomBoardService.CancelApartmentMaintenanceAsync(
                apartmentId,
                hotelId,
                maintenanceId,
                cancellationToken);

            if (!ok && string.Equals(code, "NOT_FOUND", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { success = false, code });
            }

            if (!ok)
            {
                return BadRequest(new { success = false, code });
            }

            return Ok(new { success = true });
        }

        /// <summary>
        /// Quick housekeeping / maintenance updates from the room board card context menu.
        /// Modes: setCleaning, clearCleaning, setMaintenance, clearMaintenance.
        /// </summary>
        [HttpPost("apartments/{apartmentId:int}/quick-state")]
        [RequirePermission("room_board.update_status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ApplyApartmentQuickState(
            [FromRoute] int apartmentId,
            [FromBody] RoomBoardQuickStateRequestDto? body,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            var mode = body?.Mode ?? string.Empty;
            var (ok, code) = await _roomBoardService.ApplyApartmentQuickStateAsync(
                apartmentId,
                hotelId,
                mode,
                cancellationToken);

            if (!ok && string.Equals(code, "NOT_FOUND", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { success = false, code, message = "Apartment not found." });
            }

            if (!ok)
            {
                return BadRequest(new { success = false, code });
            }

            return Ok(new { success = true });
        }
    }
}
