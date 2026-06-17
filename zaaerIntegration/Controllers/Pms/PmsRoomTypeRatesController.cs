#pragma warning disable CS1591

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.DTOs.Pms.Property;
using zaaerIntegration.Security;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Controllers.Pms
{
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/property")]
    [Produces("application/json")]
    public sealed class PmsRoomTypeRatesController : ControllerBase
    {
        private readonly IPmsRoomTypeRatesService _ratesService;

        public PmsRoomTypeRatesController(IPmsRoomTypeRatesService ratesService)
        {
            _ratesService = ratesService;
        }

        [HttpGet("room-type-rates")]
        [RequirePermission("property.rates.view")]
        public async Task<IActionResult> ListRoomTypeRates(CancellationToken cancellationToken)
        {
            var data = await _ratesService.ListRoomTypeRatesAsync(cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpPut("room-type-rates/{rateId:int}")]
        [RequirePermission("property.rates.manage")]
        public async Task<IActionResult> UpdateRoomTypeRate(
            int rateId,
            [FromBody] PmsUpdateRoomTypeRateDto dto,
            CancellationToken cancellationToken)
        {
            var data = await _ratesService.UpdateRoomTypeRateAsync(rateId, dto, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Room type not found." });
            }

            return Ok(new { success = true, data });
        }

        [HttpGet("rates-calendar")]
        [RequirePermission("property.rates.view")]
        public async Task<IActionResult> GetRatesCalendar(
            [FromQuery] string? fromDate,
            [FromQuery] string? toDate,
            CancellationToken cancellationToken)
        {
            var data = await _ratesService.GetRatesCalendarAsync(fromDate, toDate, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpPut("rates-calendar/daily")]
        [RequirePermission("property.rates.manage")]
        public async Task<IActionResult> UpsertDailyRates(
            [FromBody] PmsUpsertDailyRatesDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                await _ratesService.UpsertDailyRatesAsync(dto, cancellationToken);
                return Ok(new { success = true, message = "Rates updated." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                var detail = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to save daily rates.",
                    detail
                });
            }
        }
    }
}
