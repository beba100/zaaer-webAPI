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
    [Route("api/v1/pms/reservation-notes")]
    [Produces("application/json")]
    public sealed class ReservationNotesController : ControllerBase
    {
        private readonly IReservationNotesService _notesService;
        private readonly ICurrentUserContext _currentUser;

        public ReservationNotesController(
            IReservationNotesService notesService,
            ICurrentUserContext currentUser)
        {
            _notesService = notesService;
            _currentUser = currentUser;
        }

        [HttpGet]
        [RequirePermission("reservations.view")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> List(
            [FromQuery] int reservationId,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            var user = ResolveCurrentUser();
            var result = await _notesService.ListAsync(reservationId, hotelId, user.UserId, cancellationToken);
            if (result == null)
            {
                return NotFound(new { success = false, message = "Reservation not found." });
            }

            return Ok(new { success = true, data = result });
        }

        [HttpGet("count")]
        [RequirePermission("reservations.view")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Count(
            [FromQuery] int reservationId,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            var count = await _notesService.CountAsync(reservationId, hotelId, cancellationToken);
            return Ok(new { success = true, data = new { count } });
        }

        [HttpPost]
        [RequirePermission("reservations.update")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Create(
            [FromForm] ReservationNoteFormRequest request,
            IFormFile? attachment,
            CancellationToken cancellationToken)
        {
            var user = ResolveCurrentUser();
            var dto = new CreateReservationNoteDto
            {
                ReservationId = request.ReservationId,
                HotelId = request.HotelId,
                NoteType = request.NoteType,
                NoteText = request.NoteText ?? string.Empty
            };

            return await ExecuteNoteMutationAsync(
                request.ReservationId,
                request.HotelId,
                user.UserId,
                () => _notesService.CreateAsync(dto, user.UserId, user.DisplayName, attachment, cancellationToken),
                cancellationToken);
        }

        [HttpPut("{noteId:int}")]
        [RequirePermission("reservations.update")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(
            int noteId,
            [FromForm] ReservationNoteFormRequest request,
            IFormFile? attachment,
            CancellationToken cancellationToken)
        {
            var user = ResolveCurrentUser();
            var dto = new UpdateReservationNoteDto
            {
                ReservationId = request.ReservationId,
                HotelId = request.HotelId,
                NoteType = request.NoteType,
                NoteText = request.NoteText ?? string.Empty,
                RemoveAttachment = request.RemoveAttachment
            };

            return await ExecuteNoteMutationAsync(
                request.ReservationId,
                request.HotelId,
                user.UserId,
                () => _notesService.UpdateAsync(noteId, dto, user.UserId, attachment, cancellationToken),
                cancellationToken);
        }

        [HttpDelete("{noteId:int}")]
        [RequirePermission("reservations.update")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(
            int noteId,
            [FromQuery] int reservationId,
            [FromQuery] int? hotelId,
            CancellationToken cancellationToken)
        {
            try
            {
                var user = ResolveCurrentUser();
                var deleted = await _notesService.DeleteAsync(
                    noteId,
                    reservationId,
                    hotelId,
                    user.UserId,
                    cancellationToken);

                if (!deleted)
                {
                    return NotFound(new { success = false, message = "Note or reservation not found." });
                }

                var list = await _notesService.ListAsync(reservationId, hotelId, user.UserId, cancellationToken);
                return Ok(new { success = true, data = list });
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

        private async Task<IActionResult> ExecuteNoteMutationAsync(
            int reservationRouteId,
            int? hotelId,
            int? currentUserId,
            Func<Task<ReservationNoteDto?>> action,
            CancellationToken cancellationToken)
        {
            try
            {
                var note = await action();
                if (note == null)
                {
                    return NotFound(new { success = false, message = "Reservation not found." });
                }

                var list = await _notesService.ListAsync(reservationRouteId, hotelId, currentUserId, cancellationToken);
                return Ok(new { success = true, data = new { note, list } });
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

        private (int? UserId, string? DisplayName) ResolveCurrentUser()
        {
            var userId = PmsCurrentUser.ResolveUserId(_currentUser);
            var displayName = PmsCurrentUser.ResolveDisplayName(_currentUser);
            return (userId, displayName);
        }
    }
}
