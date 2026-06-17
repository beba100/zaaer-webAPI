#pragma warning disable CS1591

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zaaerIntegration.DTOs.BookingEngine;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Controllers.Public
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/v1/public/booking")]
    [Produces("application/json")]
    public sealed class BookingEnginePublicController : ControllerBase
    {
        private readonly IBookingEngineService _bookingEngineService;

        public BookingEnginePublicController(IBookingEngineService bookingEngineService)
        {
            _bookingEngineService = bookingEngineService;
        }

        [HttpGet("hotels")]
        public async Task<IActionResult> GetHotels(CancellationToken cancellationToken)
        {
            var data = await _bookingEngineService.GetPublicHotelsAsync(cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile([FromQuery] string hotel, CancellationToken cancellationToken)
        {
            var data = await _bookingEngineService.GetHotelProfileAsync(hotel, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Hotel not found or booking disabled." });
            }

            return Ok(new { success = true, data });
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] BookingSearchRequestDto request, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _bookingEngineService.SearchAsync(request, cancellationToken);
                if (data == null)
                {
                    return NotFound(new { success = false, message = "Hotel not found or booking disabled." });
                }

                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("guest-lookup")]
        public async Task<IActionResult> LookupReturningGuest(
            [FromQuery] string hotel,
            [FromQuery] string? phone,
            CancellationToken cancellationToken)
        {
            var data = await _bookingEngineService.LookupReturningGuestAsync(hotel, phone, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpPost("validate-coupon")]
        public async Task<IActionResult> ValidateCoupon(
            [FromBody] BookingCouponValidateRequestDto request,
            CancellationToken cancellationToken)
        {
            var data = await _bookingEngineService.ValidateCouponAsync(request, cancellationToken);
            if (data == null)
            {
                return BadRequest(new { success = false, message = "Invalid request." });
            }

            return Ok(new { success = data.Valid, message = data.Message, data });
        }

        [HttpPost("confirm")]
        public async Task<IActionResult> Confirm([FromBody] BookingConfirmRequestDto request, CancellationToken cancellationToken)
        {
            var result = await _bookingEngineService.ConfirmAsync(request, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.Message, data = result });
            }

            return Ok(new { success = true, message = result.Message, data = result });
        }
    }
}
