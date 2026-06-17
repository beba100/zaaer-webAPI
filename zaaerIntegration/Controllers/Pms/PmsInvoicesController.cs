#pragma warning disable CS1591

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Reporting.Abstractions;
using zaaerIntegration.Security;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Controllers.Pms
{
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/invoices")]
    public sealed class PmsInvoicesController : ControllerBase
    {
        private readonly IPmsInvoiceService _service;
        private readonly IReportRegistry _reportRegistry;
        private readonly ILogger<PmsInvoicesController> _logger;

        public PmsInvoicesController(
            IPmsInvoiceService service,
            IReportRegistry reportRegistry,
            ILogger<PmsInvoicesController> logger)
        {
            _service = service;
            _reportRegistry = reportRegistry;
            _logger = logger;
        }

        [HttpGet("reservation/{reservationId:int}")]
        [RequirePermission("finance.invoice.view")]
        public async Task<IActionResult> ListByReservation(
            [FromRoute] int reservationId,
            CancellationToken cancellationToken)
        {
            var data = await _service.ListByReservationAsync(reservationId, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("reservation/{reservationId:int}/create-context")]
        [RequirePermission("finance.invoice.create")]
        public async Task<IActionResult> GetCreateContext(
            [FromRoute] int reservationId,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.GetCreateContextAsync(reservationId, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("{invoiceId:int}/adjustments")]
        [RequirePermission("finance.credit_note.view")]
        public async Task<IActionResult> ListAdjustments(
            [FromRoute] int invoiceId,
            CancellationToken cancellationToken)
        {
            var data = await _service.ListAdjustmentsByInvoiceAsync(invoiceId, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpPost]
        [RequirePermission("finance.invoice.create")]
        public async Task<IActionResult> Create(
            [FromBody] PmsCreateInvoiceDto dto,
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
                    message = "Invoice created successfully.",
                    data = created
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS invoice create failed");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("by-zaaer/{zaaerId:int}/print")]
        [RequirePermission("finance.invoice.view")]
        [Produces("application/pdf", "application/json")]
        public Task<IActionResult> PrintByZaaerId(
            [FromRoute] int zaaerId,
            CancellationToken cancellationToken) =>
            RenderInvoicePrintAsync(
                new Dictionary<string, object> { ["invoiceZaaerId"] = zaaerId },
                zaaerId,
                cancellationToken);

        [HttpGet("{invoiceId:int}/print")]
        [RequirePermission("finance.invoice.view")]
        [Produces("application/pdf", "application/json")]
        public Task<IActionResult> Print(
            [FromRoute] int invoiceId,
            CancellationToken cancellationToken) =>
            RenderInvoicePrintAsync(
                new Dictionary<string, object> { ["invoiceId"] = invoiceId },
                invoiceId,
                cancellationToken);

        private async Task<IActionResult> RenderInvoicePrintAsync(
            Dictionary<string, object> parameters,
            int logId,
            CancellationToken cancellationToken)
        {
            try
            {
                var provider = _reportRegistry.ResolveProvider(ReportKeys.Invoice, ReportVersions.Invoice_v1);
                var context = new ReportContext
                {
                    ReportKey = ReportKeys.Invoice,
                    ReportVersion = ReportVersions.Invoice_v1,
                    Parameters = parameters
                };

                var result = await provider.RenderAsync(context, ReportExportFormat.Pdf, cancellationToken);
                return File(result.Content, result.MimeType, result.FileName);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS invoice print failed for id={InvoiceId}", logId);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}
