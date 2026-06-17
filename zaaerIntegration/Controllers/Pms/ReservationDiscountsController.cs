#pragma warning disable CS1591

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zaaerIntegration.DTOs.Pms.ReservationDetail;
using zaaerIntegration.Security;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Controllers.Pms
{
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/reservation-discounts")]
    [Produces("application/json")]
    public sealed class ReservationDiscountsController : ControllerBase
    {
        private readonly IReservationDetailService _reservationDetailService;

        public ReservationDiscountsController(IReservationDetailService reservationDetailService)
        {
            _reservationDetailService = reservationDetailService;
        }

        [HttpPost]
        [RequirePermission("reservations.discount")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ApplyDiscount(
            [FromBody] CreateReservationDiscountDto request,
            CancellationToken cancellationToken)
        {
            return await ExecuteDiscountMutationAsync(
                () => _reservationDetailService.ApplyDiscountAsync(request, cancellationToken));
        }

        [HttpPut("{discountId:int}")]
        [RequirePermission("reservations.discount")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateDiscount(
            int discountId,
            [FromBody] UpdateReservationDiscountDto request,
            CancellationToken cancellationToken)
        {
            return await ExecuteDiscountMutationAsync(
                () => _reservationDetailService.UpdateDiscountAsync(discountId, request, cancellationToken));
        }

        [HttpDelete("{discountId:int}")]
        [RequirePermission("reservations.discount")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteDiscount(
            int discountId,
            [FromQuery] int reservationId,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            return await ExecuteDiscountMutationAsync(
                () => _reservationDetailService.DeleteDiscountAsync(discountId, reservationId, hotelId, cancellationToken));
        }

        private async Task<IActionResult> ExecuteDiscountMutationAsync(
            Func<Task<ReservationDiscountApplyResultDto?>> action)
        {
            try
            {
                var result = await action();
                if (result == null)
                {
                    return NotFound(new { success = false, message = "Reservation or discount not found." });
                }

                return Ok(new { success = true, data = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}
