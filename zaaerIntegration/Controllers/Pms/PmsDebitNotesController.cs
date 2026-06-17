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
    [Route("api/v1/pms/debit-notes")]
    [Produces("application/json")]
    public sealed class PmsDebitNotesController : ControllerBase
    {
        private readonly IPmsDebitNoteService _service;
        private readonly ILogger<PmsDebitNotesController> _logger;

        public PmsDebitNotesController(IPmsDebitNoteService service, ILogger<PmsDebitNotesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet("invoice/{invoiceId:int}")]
        [RequirePermission("finance.debit_note.view")]
        public async Task<IActionResult> ListByInvoice(
            [FromRoute] int invoiceId,
            CancellationToken cancellationToken)
        {
            var data = await _service.ListByInvoiceAsync(invoiceId, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpPost]
        [RequirePermission("finance.debit_note.create")]
        public async Task<IActionResult> Create(
            [FromBody] PmsCreateDebitNoteDto dto,
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
                    message = "Debit note created successfully.",
                    data = created
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS debit note create failed");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}
