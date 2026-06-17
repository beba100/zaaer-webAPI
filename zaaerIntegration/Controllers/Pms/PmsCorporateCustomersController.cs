#pragma warning disable CS1591

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zaaerIntegration.DTOs.Request;
using zaaerIntegration.DTOs.Response;
using zaaerIntegration.Security;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Controllers.Pms
{
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/corporate-customers")]
    [Produces("application/json")]
    public sealed class PmsCorporateCustomersController : ControllerBase
    {
        private readonly IPmsCorporateCustomerService _pmsCorporateCustomerService;
        private readonly ILogger<PmsCorporateCustomersController> _logger;

        public PmsCorporateCustomersController(
            IPmsCorporateCustomerService pmsCorporateCustomerService,
            ILogger<PmsCorporateCustomersController> logger)
        {
            _pmsCorporateCustomerService = pmsCorporateCustomerService;
            _logger = logger;
        }

        [HttpGet("for-picker")]
        [RequirePermission("reservations.view")]
        [ProducesResponseType(typeof(CorporatePickerResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetCorporateCustomersForPicker(
            [FromQuery] int? hotelId,
            [FromQuery] string? hotelCode,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _pmsCorporateCustomerService.GetForPickerAsync(hotelId, hotelCode, cancellationToken);
                if (data.ResolvedHotelId is not > 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Hotel scope is required. Pass hotelId or hotelCode."
                    });
                }

                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS load corporate customers for picker failed");
                return StatusCode(500, new { success = false, message = "Could not load corporate customers for picker." });
            }
        }

        /// <summary>
        /// Load corporate customer by internal <c>corporate_id</c> or integration <c>zaaer_id</c>.
        /// </summary>
        [HttpGet("{id:int}")]
        [RequirePermission("reservations.view")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCorporateCustomer(
            [FromRoute] int id,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            var data = await _pmsCorporateCustomerService.GetByZaaerOrCorporateIdAsync(id, hotelId, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Corporate customer not found." });
            }

            return Ok(new
            {
                success = true,
                message = "Corporate customer loaded successfully.",
                data
            });
        }

        [HttpPost]
        [RequirePermission("reservations.company_add")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateCorporateCustomer(
            [FromBody] CreateCorporateCustomerDto dto,
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
                var data = await _pmsCorporateCustomerService.CreateAsync(dto, cancellationToken);
                var routeKey = data.ZaaerId is > 0 ? data.ZaaerId.Value : data.CorporateId;
                return CreatedAtAction(
                    nameof(GetCorporateCustomer),
                    new { id = routeKey, hotelId = dto.HotelId },
                    new
                    {
                        success = true,
                        message = "Corporate customer created successfully.",
                        data
                    });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS create corporate customer failed");
                return StatusCode(500, new { success = false, message = "Could not create corporate customer." });
            }
        }

        [HttpPut("{id:int}")]
        [RequirePermission("reservations.company_add")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateCorporateCustomer(
            [FromRoute] int id,
            [FromBody] UpdateCorporateCustomerDto dto,
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

            dto.CorporateId = id;

            try
            {
                var data = await _pmsCorporateCustomerService.UpdateAsync(id, dto, hotelId, cancellationToken);
                if (data == null)
                {
                    return NotFound(new { success = false, message = "Corporate customer not found." });
                }

                return Ok(new
                {
                    success = true,
                    message = "Corporate customer updated successfully.",
                    data
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS update corporate customer {CorporateId} failed", id);
                return StatusCode(500, new { success = false, message = "Could not update corporate customer." });
            }
        }
    }
}
