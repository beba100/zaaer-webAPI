using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zaaerIntegration.DTOs.Pms.ActivityLog;
using zaaerIntegration.Security;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Controllers.Pms
{
    [ApiController]
    [Authorize]
    [Route("api/v1/pms")]
    [Produces("application/json")]
    public sealed class PmsActivityLogsController : ControllerBase
    {
        private readonly IReservationActivityLogQueryService _queryService;

        public PmsActivityLogsController(IReservationActivityLogQueryService queryService)
        {
            _queryService = queryService;
        }

        [HttpGet("reservations/{reservationId:int}/activity-logs")]
        [RequirePermission("reservations.activity_log_view")]
        public async Task<IActionResult> ListForReservation(
            [FromRoute] int reservationId,
            [FromQuery] int? hotelId,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50,
            CancellationToken cancellationToken = default)
        {
            var items = await _queryService.ListForReservationAsync(
                reservationId,
                hotelId,
                skip,
                take,
                cancellationToken);

            return Ok(new { success = true, data = items });
        }

        [HttpGet("activity-logs")]
        [RequirePermission("reservations.activity_log_view")]
        public async Task<IActionResult> Search(
            [FromQuery] PmsActivityLogQueryDto query,
            CancellationToken cancellationToken = default)
        {
            var items = await _queryService.SearchAsync(query, cancellationToken);
            return Ok(new { success = true, data = items });
        }
    }
}
