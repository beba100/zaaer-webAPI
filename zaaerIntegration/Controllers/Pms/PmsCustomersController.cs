#pragma warning disable CS1591

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zaaerIntegration.DTOs.Request;
using zaaerIntegration.Security;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Controllers.Pms
{
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/customers")]
    [Produces("application/json")]
    public sealed class PmsCustomersController : ControllerBase
    {
        private readonly IPmsCustomerService _pmsCustomerService;
        private readonly ILogger<PmsCustomersController> _logger;

        public PmsCustomersController(
            IPmsCustomerService pmsCustomerService,
            ILogger<PmsCustomersController> logger)
        {
            _pmsCustomerService = pmsCustomerService;
            _logger = logger;
        }

        [HttpGet]
        [RequirePermission("guests.list")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCustomers(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? searchMode = null,
            [FromQuery] int? nationalityId = null,
            [FromQuery] int? guestCategoryId = null)
        {
            pageSize = Math.Clamp(pageSize, 1, 500);
            pageNumber = Math.Max(1, pageNumber);

            var (customers, totalCount) = await _pmsCustomerService.GetPagedAsync(
                pageNumber,
                pageSize,
                searchTerm,
                searchMode,
                nationalityId,
                guestCategoryId);

            return Ok(new
            {
                customers,
                totalCount,
                pageNumber,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }

        /// <summary>
        /// Load customer by internal <c>customer_id</c> or integration <c>zaaer_id</c>.
        /// </summary>
        [HttpGet("{id:int}")]
        [RequirePermission("guests.view")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCustomer(
            [FromRoute] int id,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            var data = await _pmsCustomerService.GetByZaaerOrCustomerIdAsync(id, hotelId, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Customer not found." });
            }

            return Ok(new
            {
                success = true,
                message = "Customer loaded successfully.",
                data
            });
        }

        [HttpPost]
        [RequirePermission("guests.create")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateCustomer(
            [FromBody] CreateCustomerDto dto,
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
                var data = await _pmsCustomerService.CreateAsync(dto, cancellationToken);
                return CreatedAtAction(
                    nameof(GetCustomer),
                    new { id = data.ZaaerId ?? data.CustomerId, hotelId = dto.HotelId },
                    new
                    {
                        success = true,
                        message = "Customer created successfully.",
                        data
                    });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS create customer failed");
                return StatusCode(500, new { success = false, message = "Could not create customer." });
            }
        }

        [HttpPut("{id:int}")]
        [RequirePermission("guests.update")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateCustomer(
            [FromRoute] int id,
            [FromBody] UpdateCustomerDto dto,
            [FromQuery] int? hotelId,
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

            dto.CustomerId = id;

            try
            {
                var data = await _pmsCustomerService.UpdateAsync(id, dto, hotelId, cancellationToken);
                if (data == null)
                {
                    return NotFound(new { success = false, message = "Customer not found." });
                }

                return Ok(new
                {
                    success = true,
                    message = "Customer updated successfully.",
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS update customer {CustomerId} failed", id);
                return StatusCode(500, new { success = false, message = "Could not update customer." });
            }
        }
    }
}
