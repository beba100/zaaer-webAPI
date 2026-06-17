#pragma warning disable CS1591

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Security;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Controllers.Pms
{
    /// <summary>
    /// Hotel expenses for the PMS UI (<c>X-Hotel-Code</c> tenant scope).
    /// Legacy supervisor/VoM flows remain on <c>/api/Expense</c> until migrated.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/expenses")]
    [Produces("application/json")]
    public sealed class PmsExpensesController : ControllerBase
    {
        private readonly IPmsExpenseService _service;
        private readonly ICurrentUserContext _currentUser;
        private readonly ILogger<PmsExpensesController> _logger;

        public PmsExpensesController(
            IPmsExpenseService service,
            ICurrentUserContext currentUser,
            ILogger<PmsExpensesController> logger)
        {
            _service = service;
            _currentUser = currentUser;
            _logger = logger;
        }

        [HttpGet]
        [RequirePermission("finance.expense.view")]
        public async Task<IActionResult> List(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            CancellationToken cancellationToken)
        {
            var data = await _service.ListAsync(fromDate, toDate, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("companies")]
        [RequirePermission("finance.expense.view")]
        public async Task<IActionResult> SearchCompanies(
            [FromQuery] string? search,
            CancellationToken cancellationToken)
        {
            var data = await _service.SearchCompaniesAsync(search, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("companies/lookup")]
        [RequirePermission("finance.expense.view")]
        public async Task<IActionResult> LookupCompanyByTaxNumber(
            [FromQuery] string taxNumber,
            CancellationToken cancellationToken)
        {
            var data = await _service.LookupCompanyByTaxNumberAsync(taxNumber, cancellationToken);
            return Ok(new { success = true, found = data != null, data });
        }

        [HttpGet("categories")]
        [RequirePermission("finance.expense.view")]
        public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
        {
            var data = await _service.GetCategoriesAsync(cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("tax-config")]
        [RequirePermission("finance.expense.view")]
        public async Task<IActionResult> GetTaxConfig(CancellationToken cancellationToken)
        {
            var data = await _service.GetTaxConfigAsync(cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("{expenseId:long}")]
        [RequirePermission("finance.expense.view")]
        public async Task<IActionResult> GetById([FromRoute] long expenseId, CancellationToken cancellationToken)
        {
            var data = await _service.GetByIdAsync(expenseId, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Expense not found." });
            }

            return Ok(new { success = true, data });
        }

        [HttpPost]
        [RequirePermission("finance.expense.create")]
        public async Task<IActionResult> Create(
            [FromBody] PmsCreateExpenseDto dto,
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
                    message = "Expense created successfully.",
                    data
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS expense create failed");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPut("{expenseId:long}")]
        [RequirePermission("finance.expense.update")]
        public async Task<IActionResult> Update(
            [FromRoute] long expenseId,
            [FromBody] PmsUpdateExpenseDto dto,
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
                var data = await _service.UpdateAsync(expenseId, dto, cancellationToken);
                if (data == null)
                {
                    return NotFound(new { success = false, message = "Expense not found." });
                }

                return Ok(new
                {
                    success = true,
                    message = "Expense updated successfully.",
                    data
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS expense update failed for ExpenseId={ExpenseId}", expenseId);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("{expenseId:long}")]
        [RequirePermission("finance.expense.update")]
        public async Task<IActionResult> Delete([FromRoute] long expenseId, CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _service.DeleteAsync(expenseId, cancellationToken);
                if (!deleted)
                {
                    return NotFound(new { success = false, message = "Expense not found." });
                }

                return Ok(new { success = true, message = "Expense deleted successfully." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS expense delete failed for ExpenseId={ExpenseId}", expenseId);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("{expenseId:long}/approve")]
        [RequirePermission("finance.expense.approve")]
        public async Task<IActionResult> Approve(
            [FromRoute] long expenseId,
            [FromBody] PmsApproveExpenseRequestDto dto,
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

            if (!_currentUser.UserId.HasValue || _currentUser.UserId.Value <= 0)
            {
                return Unauthorized(new { success = false, message = "User context is required for approval." });
            }

            try
            {
                var data = await _service.ApproveAsync(
                    expenseId,
                    dto,
                    _currentUser.UserId.Value,
                    cancellationToken);

                if (data == null)
                {
                    return NotFound(new { success = false, message = "Expense not found." });
                }

                return Ok(new
                {
                    success = true,
                    message = "Expense approval updated successfully.",
                    data
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS expense approve failed for ExpenseId={ExpenseId}", expenseId);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("{expenseId:long}/approval-history")]
        [RequirePermission("finance.expense.view")]
        public async Task<IActionResult> GetApprovalHistory(
            [FromRoute] long expenseId,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.GetApprovalHistoryAsync(expenseId, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("{expenseId:long}/images")]
        [RequirePermission("finance.expense.view")]
        public async Task<IActionResult> GetImages([FromRoute] long expenseId, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.GetImagesAsync(expenseId, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("{expenseId:long}/images")]
        [RequirePermission("finance.expense.create")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadImages(
            [FromRoute] long expenseId,
            [FromForm] List<IFormFile> images,
            CancellationToken cancellationToken)
        {
            if (images == null || images.Count == 0)
            {
                return BadRequest(new { success = false, message = "No images provided." });
            }

            try
            {
                var data = await _service.UploadImagesAsync(expenseId, images, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS expense image upload failed for ExpenseId={ExpenseId}", expenseId);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("{expenseId:long}/images/{imageId:int}")]
        [RequirePermission("finance.expense.update")]
        public async Task<IActionResult> DeleteImage(
            [FromRoute] long expenseId,
            [FromRoute] int imageId,
            CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _service.DeleteImageAsync(expenseId, imageId, cancellationToken);
                if (!deleted)
                {
                    return NotFound(new { success = false, message = "Image not found." });
                }

                return Ok(new { success = true, message = "Image deleted successfully." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
        }
    }
}
