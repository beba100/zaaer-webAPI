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
    [Route("api/v1/pms/payment-receipts")]
    [Produces("application/json")]
    public sealed class PmsPaymentReceiptsController : ControllerBase
    {
        private readonly IPmsPaymentReceiptService _service;
        private readonly ILogger<PmsPaymentReceiptsController> _logger;

        public PmsPaymentReceiptsController(
            IPmsPaymentReceiptService service,
            ILogger<PmsPaymentReceiptsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// List payment receipts for a reservation (internal <c>reservation_id</c>).
        /// </summary>
        [HttpGet("reservation/{reservationId:int}")]
        [RequirePermission("payments.list")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ListByReservation(
            [FromRoute] int reservationId,
            [FromQuery] string? receiptType,
            [FromQuery] string? kind,
            CancellationToken cancellationToken)
        {
            var data = await _service.ListByReservationAsync(
                reservationId,
                receiptType,
                kind,
                cancellationToken);
            return Ok(new { success = true, data });
        }

        /// <summary>Load a single payment receipt by unique <c>zaaer_id</c> (report popups / view-only).</summary>
        [HttpGet("by-zaaer/{zaaerId:int}")]
        [RequireAnyPermission("payments.view", "payments.list")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByZaaerId(
            [FromRoute] int zaaerId,
            CancellationToken cancellationToken)
        {
            var data = await _service.GetByZaaerIdAsync(zaaerId, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Payment receipt not found." });
            }

            return Ok(new { success = true, data });
        }

        /// <summary>Last rent receipt with period columns for receipt popup hint.</summary>
        [HttpGet("reservation/{reservationId:int}/last-rent")]
        [RequirePermission("payments.create")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLastRentReceipt(
            [FromRoute] int reservationId,
            CancellationToken cancellationToken)
        {
            var data = await _service.GetLastRentReceiptAsync(reservationId, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpPost]
        [RequirePermission("payments.create")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create(
            [FromBody] PmsCreatePaymentReceiptDto dto,
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
                    message = "Payment receipt created successfully.",
                    data = created
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS payment receipt create failed");
                return StatusCode(500, new { success = false, message = "Failed to create payment receipt." });
            }
        }

        /// <summary>
        /// Update by unique <c>payment_receipts.zaaer_id</c> (not internal <c>receipt_id</c>).
        /// </summary>
        [HttpPut("by-zaaer/{zaaerId:int}")]
        [RequirePermission("payments.create")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateByZaaerId(
            [FromRoute] int zaaerId,
            [FromBody] PmsUpdatePaymentReceiptDto dto,
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
                return Ok(new
                {
                    success = true,
                    message = "Payment receipt updated successfully.",
                    data = updated
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS payment receipt update failed for ZaaerId {ZaaerId}", zaaerId);
                return StatusCode(500, new { success = false, message = "Failed to update payment receipt." });
            }
        }

        /// <summary>Cancel by unique <c>zaaer_id</c> (sets <c>receipt_status</c> to cancelled).</summary>
        [HttpPost("by-zaaer/{zaaerId:int}/cancel")]
        [RequireAnyPermission("payments.cancel", "payments.refund_voucher.cancel")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CancelByZaaerId(
            [FromRoute] int zaaerId,
            [FromBody] PmsCancelPaymentReceiptDto dto,
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
                return Ok(new
                {
                    success = true,
                    message = "Payment receipt cancelled successfully.",
                    data = cancelled
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (ReservationPermissionDeniedException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    error = "ليس لديك صلاحية لهذا الإجراء.",
                    permissionCode = ex.PermissionCode
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS payment receipt cancel failed for ZaaerId {ZaaerId}", zaaerId);
                return StatusCode(500, new { success = false, message = "Failed to cancel payment receipt." });
            }
        }
    }
}
