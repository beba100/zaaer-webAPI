#pragma warning disable CS1591

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Security;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Controllers.Pms
{
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/promissory-notes")]
    [Produces("application/json")]
    public sealed class PmsPromissoryNotesController : ControllerBase
    {
        private readonly IPmsPromissoryNoteService _service;
        private readonly ILogger<PmsPromissoryNotesController> _logger;

        public PmsPromissoryNotesController(
            IPmsPromissoryNoteService service,
            ILogger<PmsPromissoryNotesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet("reservation/{reservationId:int}")]
        [RequirePermission("payments.list")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ListByReservation(
            [FromRoute] int reservationId,
            CancellationToken cancellationToken)
        {
            var data = await _service.ListByReservationAsync(reservationId, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpPost]
        [RequirePermission("finance.promissory.create")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create(
            [FromBody] PmsCreatePromissoryNoteDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid request.",
                    errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))
                });
            }

            try
            {
                var created = await _service.CreateAsync(dto, cancellationToken);
                return Created(string.Empty, new
                {
                    success = true,
                    message = "Promissory note created successfully.",
                    data = created
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create promissory note for reservation {ReservationId}", dto.ReservationId);
                return StatusCode(500, new { success = false, message = "Failed to create promissory note." });
            }
        }

        [HttpPut("by-zaaer/{zaaerId:int}")]
        [RequirePermission("finance.promissory_note.edit")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateByZaaerId(
            [FromRoute] int zaaerId,
            [FromBody] PmsUpdatePromissoryNoteDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid request.",
                    errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))
                });
            }

            try
            {
                var updated = await _service.UpdateByZaaerIdAsync(zaaerId, dto, cancellationToken);
                return Ok(new { success = true, message = "Promissory note updated.", data = updated });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update promissory note ZaaerId {ZaaerId}", zaaerId);
                return StatusCode(500, new { success = false, message = "Failed to update promissory note." });
            }
        }

        [HttpPost("by-zaaer/{zaaerId:int}/cancel")]
        [RequirePermission("finance.promissory_note.cancel")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CancelByZaaerId(
            [FromRoute] int zaaerId,
            [FromBody] PmsCancelPromissoryNoteDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid request.",
                    errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))
                });
            }

            try
            {
                var cancelled = await _service.CancelByZaaerIdAsync(zaaerId, dto, cancellationToken);
                return Ok(new { success = true, message = "Promissory note cancelled.", data = cancelled });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel promissory note ZaaerId {ZaaerId}", zaaerId);
                return StatusCode(500, new { success = false, message = "Failed to cancel promissory note." });
            }
        }
    }
}
