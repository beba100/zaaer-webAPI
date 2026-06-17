#pragma warning disable CS1591

using Microsoft.AspNetCore.Http;
using zaaerIntegration.DTOs.Pms.ReservationDetail;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IReservationNotesService
    {
        Task<ReservationNotesListDto?> ListAsync(
            int reservationRouteId,
            int? hotelId,
            int? currentUserId,
            CancellationToken cancellationToken = default);

        Task<int> CountAsync(
            int reservationRouteId,
            int? hotelId,
            CancellationToken cancellationToken = default);

        Task<ReservationNoteDto?> CreateAsync(
            CreateReservationNoteDto request,
            int? currentUserId,
            string? currentUserDisplayName,
            IFormFile? attachment = null,
            CancellationToken cancellationToken = default);

        Task<ReservationNoteDto?> UpdateAsync(
            int noteId,
            UpdateReservationNoteDto request,
            int? currentUserId,
            IFormFile? attachment = null,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(
            int noteId,
            int reservationRouteId,
            int? hotelId,
            int? currentUserId,
            CancellationToken cancellationToken = default);
    }
}
