#pragma warning disable CS1591

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.DTOs.Pms.ReservationDetail;
using zaaerIntegration.Security;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Controllers.Pms
{
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/reservations")]
    [Produces("application/json")]
    public sealed class ReservationDetailController : ControllerBase
    {
        private readonly IReservationDetailService _reservationDetailService;
        private readonly IReservationPeriodService _reservationPeriodService;
        private readonly IPmsHallEventService _hallEventService;
        private readonly ICurrentUserContext _currentUser;

        public ReservationDetailController(
            IReservationDetailService reservationDetailService,
            IReservationPeriodService reservationPeriodService,
            IPmsHallEventService hallEventService,
            ICurrentUserContext currentUser)
        {
            _reservationDetailService = reservationDetailService;
            _reservationPeriodService = reservationPeriodService;
            _hallEventService = hallEventService;
            _currentUser = currentUser;
        }

        /// <summary>
        /// Load reservation detail by Zaaer id (preferred) or internal reservation id. Optional <paramref name="hotelId"/> scopes the row for multi-tenant safety.
        /// </summary>
        [HttpGet("{id:int}")]
        [RequirePermission("reservations.view")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetReservationDetail(
            [FromRoute] int id,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            var data = await _reservationDetailService.GetByZaaerOrReservationIdAsync(id, hotelId, cancellationToken);

            if (data == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Reservation not found."
                });
            }

            return Ok(new
            {
                success = true,
                message = "Reservation loaded successfully.",
                data
            });
        }

        [HttpPatch("{id:int}")]
        [RequirePermission("reservations.view")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> PatchReservationDetail(
            [FromRoute] int id,
            [FromBody] ReservationPmsPatchDto patch,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _reservationDetailService.PatchReservationAsync(id, patch, hotelId, cancellationToken);

                if (data == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Reservation not found."
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Reservation updated successfully.",
                    data
                });
            }
            catch (ReservationPermissionDeniedException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    error = "ليس لديك صلاحية لهذا الإجراء.",
                    permissionCode = ex.PermissionCode
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Create a reservation from the room board with a required guest (single API; replaces legacy draft).
        /// </summary>
        [HttpPost]
        [RequirePermission("reservations.create")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateReservation(
            [FromBody] ReservationCreateDto body,
            CancellationToken cancellationToken)
        {
            if (body.ApartmentId <= 0)
            {
                return BadRequest(new { success = false, message = "ApartmentId is required." });
            }

            try
            {
                var data = await _reservationDetailService.CreateReservationAsync(body, cancellationToken);

                if (data == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Apartment not found or could not create reservation."
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Reservation created successfully.",
                    data
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Legacy draft endpoint — removed; guest must be supplied via <see cref="CreateReservation"/>.
        /// </summary>
        [HttpPost("draft")]
        [RequirePermission("reservations.create")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult CreateReservationDraft([FromBody] ReservationDraftCreateDto body)
        {
            _ = body;
            return BadRequest(new
            {
                success = false,
                message = "reservationDetail.validation.draftDeprecated"
            });
        }

        /// <summary>
        /// Live financial snapshot for check-out (units, extras, receipts, invoices reconciled from DB).
        /// </summary>
        [HttpGet("{id:int}/checkout-snapshot")]
        [RequirePermission("reservations.view")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCheckoutSnapshot(
            [FromRoute] int id,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            var data = await _reservationDetailService.GetCheckoutSnapshotAsync(id, hotelId, cancellationToken);
            if (data == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Reservation not found."
                });
            }

            return Ok(new
            {
                success = true,
                message = "Checkout snapshot loaded.",
                data
            });
        }

        [HttpPost("{id:int}/checkout")]
        [RequirePermission("reservations.check_out")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CheckoutReservation(
            [FromRoute] int id,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            try
            {
                var ok = await _reservationDetailService.CheckoutReservationAsync(id, hotelId, cancellationToken);

                if (!ok)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Reservation not found."
                    });
                }

                var data = await _reservationDetailService.GetByZaaerOrReservationIdAsync(id, hotelId, cancellationToken);

                return Ok(new
                {
                    success = true,
                    message = "Checkout recorded successfully.",
                    data
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Cancel reservation when there are no active receipts or invoices; releases rooms like checkout.
        /// </summary>
        [HttpPost("{id:int}/cancel")]
        [RequirePermission("reservations.cancel")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelReservation(
            [FromRoute] int id,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _reservationDetailService.CancelReservationAsync(id, hotelId, cancellationToken);

                if (data == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Reservation not found."
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Reservation cancelled successfully.",
                    data
                });
            }
            catch (ReservationPermissionDeniedException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    error = "ليس لديك صلاحية لهذا الإجراء.",
                    permissionCode = ex.PermissionCode
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Check out one reservation unit line. When the reservation has a single unit, performs full reservation checkout (same rules as POST checkout).
        /// </summary>
        [HttpPost("{id:int}/units/{unitId:int}/checkout")]
        [RequirePermission("reservations.unit_check_out")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CheckoutReservationUnit(
            [FromRoute] int id,
            [FromRoute] int unitId,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _reservationDetailService.CheckoutReservationUnitAsync(id, unitId, hotelId, cancellationToken);

                if (data == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Reservation not found."
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Unit checkout recorded successfully.",
                    data
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Re-open a checked-out reservation (checked-in status and rented rooms).
        /// </summary>
        [HttpPost("{id:int}/reopen-checkin")]
        [RequirePermission("reservations.reopen")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ReopenReservationAfterCheckout(
            [FromRoute] int id,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _reservationDetailService.ReopenReservationAfterCheckoutAsync(id, hotelId, cancellationToken);

                if (data == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Reservation not found."
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Reservation re-opened for check-in.",
                    data
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("{id:int}/unit-swap")]
        [RequirePermission("reservations.unit_change")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SwapReservationUnit(
            [FromRoute] int id,
            [FromBody] ReservationUnitSwapRequestDto body,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            if (body == null)
            {
                return BadRequest(new { success = false, message = "Request body is required." });
            }

            var userId = PmsCurrentUser.ResolveUserId(_currentUser);

            try
            {
                var data = await _reservationDetailService.SwapReservationUnitAsync(
                    id,
                    body,
                    hotelId,
                    userId,
                    cancellationToken);

                if (data == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Reservation not found."
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Unit transfer recorded successfully.",
                    data
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("{id:int}/unit-day-rates")]
        [RequireAnyPermission("reservations.view", "reservations.pricing_view", "reservations.pricing_edit", "reservations.pricing_edit_after_checkin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUnitDayRates(
            [FromRoute] int id,
            [FromQuery] int? unitId,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            var data = await _reservationDetailService.GetUnitDayRatesAsync(id, unitId, hotelId, cancellationToken);
            if (data == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Reservation not found."
                });
            }

            return Ok(new
            {
                success = true,
                message = "Reservation unit day rates loaded successfully.",
                data
            });
        }

        [HttpPut("{id:int}/unit-day-rates")]
        [RequireAnyPermission("reservations.update", "reservations.pricing_edit", "reservations.pricing_edit_after_checkin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SaveUnitDayRates(
            [FromRoute] int id,
            [FromBody] ReservationUnitDayRatesSaveRequestDto request,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _reservationDetailService.SaveUnitDayRatesAsync(id, request, hotelId, cancellationToken);
                if (data == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Reservation not found."
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Reservation unit day rates saved successfully.",
                    data
                });
            }
            catch (ReservationPermissionDeniedException)
            {
                return Forbid();
            }
        }

        /// <summary>
        /// Hall property: persist hall rent gross immediately (reservation, units, event profile balance).
        /// </summary>
        [HttpPut("{id:int}/hall-rent")]
        [RequireAnyPermission("reservations.pricing_edit", "reservations.pricing_edit_after_checkin", "hall.events.manage")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateHallRent(
            [FromRoute] int id,
            [FromBody] ReservationHallRentUpdateDto request,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            try
            {
                var updated = await _hallEventService.UpdateEventAsync(
                    id,
                    new PmsUpdateHallEventDto { HallRentAmount = request.HallRentAmount },
                    cancellationToken);
                if (updated == null)
                {
                    return NotFound(new { success = false, message = "Hall event not found." });
                }

                var detail = await _reservationDetailService.GetByZaaerOrReservationIdAsync(id, hotelId, cancellationToken);
                if (detail == null)
                {
                    return NotFound(new { success = false, message = "Reservation not found." });
                }

                return Ok(new
                {
                    success = true,
                    message = "Hall rent updated successfully.",
                    data = detail
                });
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

        [HttpGet("{id:int}/periods")]
        [RequirePermission("reservations.view")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetReservationPeriods(
            [FromRoute] int id,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            var data = await _reservationPeriodService.GetPeriodsAsync(id, hotelId, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Reservation not found." });
            }

            return Ok(new { success = true, data });
        }

        [HttpPost("{id:int}/periods/initial")]
        [RequirePermission("reservations.rental_periods")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateInitialReservationPeriod(
            [FromRoute] int id,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _reservationPeriodService.CreateInitialPeriodAsync(id, hotelId, cancellationToken);
                if (data == null)
                {
                    return NotFound(new { success = false, message = "Reservation not found." });
                }

                return Ok(new { success = true, message = "Initial pricing period created.", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("{id:int}/periods/append")]
        [RequirePermission("reservations.rental_periods")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AppendReservationPeriod(
            [FromRoute] int id,
            [FromBody] ReservationPeriodAppendRequestDto body,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _reservationPeriodService.AppendPeriodAsync(id, body, hotelId, cancellationToken);
                if (data == null)
                {
                    return NotFound(new { success = false, message = "Reservation not found." });
                }

                return Ok(new { success = true, message = "Pricing period appended.", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPatch("{id:int}/periods/{periodId:int}")]
        [RequirePermission("reservations.rental_periods")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateReservationPeriod(
            [FromRoute] int id,
            [FromRoute] int periodId,
            [FromBody] ReservationPeriodUpdateRequestDto body,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _reservationPeriodService.UpdateActivePeriodAsync(
                    id,
                    periodId,
                    body,
                    hotelId,
                    cancellationToken);
                if (data == null)
                {
                    return NotFound(new { success = false, message = "Reservation not found." });
                }

                return Ok(new { success = true, message = "Pricing period updated.", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}
