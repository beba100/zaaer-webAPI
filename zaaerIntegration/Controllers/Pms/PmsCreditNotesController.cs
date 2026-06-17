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
    [Route("api/v1/pms/credit-notes")]
    [Produces("application/json")]
    public sealed class PmsCreditNotesController : ControllerBase
    {
        private readonly IPmsCreditNoteService _service;
        private readonly ILogger<PmsCreditNotesController> _logger;

        public PmsCreditNotesController(IPmsCreditNoteService service, ILogger<PmsCreditNotesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet("reservation/{reservationId:int}/count")]
        [RequirePermission("finance.credit_note.view")]
        public async Task<IActionResult> CountByReservation(
            [FromRoute] int reservationId,
            CancellationToken cancellationToken)
        {
            var count = await _service.CountByReservationAsync(reservationId, cancellationToken);
            return Ok(new { success = true, data = new { count } });
        }

        [HttpGet("reservation/{reservationId:int}")]
        [RequirePermission("finance.credit_note.view")]
        public async Task<IActionResult> ListByReservation(
            [FromRoute] int reservationId,
            CancellationToken cancellationToken)
        {
            var data = await _service.ListByReservationAsync(reservationId, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("by-zaaer/{zaaerId:int}")]
        [RequirePermission("finance.credit_note.view")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByZaaerId(
            [FromRoute] int zaaerId,
            CancellationToken cancellationToken)
        {
            var data = await _service.GetByZaaerIdAsync(zaaerId, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Credit note not found." });
            }

            return Ok(new { success = true, data });
        }

        [HttpGet("invoice/{invoiceId:int}")]
        [RequirePermission("finance.credit_note.view")]
        public async Task<IActionResult> ListByInvoice(
            [FromRoute] int invoiceId,
            CancellationToken cancellationToken)
        {
            var data = await _service.ListByInvoiceAsync(invoiceId, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpPost]
        [RequirePermission("finance.credit_note.create")]
        public async Task<IActionResult> Create(
            [FromBody] PmsCreateCreditNoteDto dto,
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
                    message = "Credit note created successfully.",
                    data = created
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS credit note create failed");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}
