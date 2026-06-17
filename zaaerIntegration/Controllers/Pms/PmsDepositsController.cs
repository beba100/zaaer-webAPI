#pragma warning disable CS1591

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Security;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Controllers.Pms
{
    /// <summary>
    /// Bank deposit receipts for the PMS UI (<c>payment_receipts</c> / <c>transfers_to_bank</c>).
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/deposits")]
    [Produces("application/json")]
    public sealed class PmsDepositsController : ControllerBase
    {
        private readonly IPmsDepositService _service;
        private readonly ILogger<PmsDepositsController> _logger;

        public PmsDepositsController(IPmsDepositService service, ILogger<PmsDepositsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        [RequirePermission("finance.deposit.view")]
        public async Task<IActionResult> List(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            CancellationToken cancellationToken)
        {
            var data = await _service.ListAsync(fromDate, toDate, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("banks")]
        [RequirePermission("finance.deposit.view")]
        public async Task<IActionResult> GetBanks(CancellationToken cancellationToken)
        {
            var data = await _service.GetBanksAsync(cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("payment-methods")]
        [RequirePermission("finance.deposit.view")]
        public async Task<IActionResult> GetPaymentMethods(CancellationToken cancellationToken)
        {
            var data = await _service.GetPaymentMethodsAsync(cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("{receiptId:int}")]
        [RequirePermission("finance.deposit.view")]
        public async Task<IActionResult> GetById([FromRoute] int receiptId, CancellationToken cancellationToken)
        {
            var data = await _service.GetByIdAsync(receiptId, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Deposit not found." });
            }

            return Ok(new { success = true, data });
        }

        [HttpPost]
        [RequirePermission("finance.deposit.create")]
        public async Task<IActionResult> Create(
            [FromBody] PmsCreateDepositDto dto,
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
                var data = await _service.CreateAsync(dto, cancellationToken);
                return Created(string.Empty, new
                {
                    success = true,
                    message = "Deposit created successfully.",
                    data
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS deposit create failed");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPut("{receiptId:int}")]
        [RequirePermission("finance.deposit.update")]
        public async Task<IActionResult> Update(
            [FromRoute] int receiptId,
            [FromBody] PmsUpdateDepositDto dto,
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
                var data = await _service.UpdateAsync(receiptId, dto, cancellationToken);
                if (data == null)
                {
                    return NotFound(new { success = false, message = "Deposit not found." });
                }

                return Ok(new { success = true, message = "Deposit updated successfully.", data });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS deposit update failed for ReceiptId={ReceiptId}", receiptId);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("{receiptId:int}")]
        [RequirePermission("finance.deposit.update")]
        public async Task<IActionResult> Delete([FromRoute] int receiptId, CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _service.DeleteAsync(receiptId, cancellationToken);
                if (!deleted)
                {
                    return NotFound(new { success = false, message = "Deposit not found." });
                }

                return Ok(new { success = true, message = "Deposit cancelled successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS deposit delete failed for ReceiptId={ReceiptId}", receiptId);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("{receiptId:int}/images")]
        [RequirePermission("finance.deposit.view")]
        public async Task<IActionResult> GetImages([FromRoute] int receiptId, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.GetImagesAsync(receiptId, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("{receiptId:int}/images")]
        [RequirePermission("finance.deposit.create")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadImages(
            [FromRoute] int receiptId,
            [FromForm] List<IFormFile> images,
            CancellationToken cancellationToken)
        {
            if (images == null || images.Count == 0)
            {
                return BadRequest(new { success = false, message = "No images provided." });
            }

            try
            {
                var data = await _service.UploadImagesAsync(receiptId, images, cancellationToken);
                return Ok(new { success = true, message = "Images uploaded successfully.", data });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS deposit image upload failed for ReceiptId={ReceiptId}", receiptId);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("{receiptId:int}/images/{imageId:int}")]
        [RequirePermission("finance.deposit.update")]
        public async Task<IActionResult> DeleteImage(
            [FromRoute] int receiptId,
            [FromRoute] int imageId,
            CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _service.DeleteImageAsync(receiptId, imageId, cancellationToken);
                if (!deleted)
                {
                    return NotFound(new { success = false, message = "Image not found." });
                }

                return Ok(new { success = true, message = "Image deleted successfully." });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
        }
    }
}
