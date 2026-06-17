using zaaerIntegration.DTOs.Pms;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IPmsPromissoryNoteService
    {
        Task<IReadOnlyList<PmsPromissoryNoteRowDto>> ListByReservationAsync(
            int reservationId,
            CancellationToken cancellationToken = default);

        Task<PmsPromissoryNoteRowDto> CreateAsync(
            PmsCreatePromissoryNoteDto dto,
            CancellationToken cancellationToken = default);

        Task<PmsPromissoryNoteRowDto> UpdateByZaaerIdAsync(
            int zaaerId,
            PmsUpdatePromissoryNoteDto dto,
            CancellationToken cancellationToken = default);

        Task<PmsPromissoryNoteRowDto> CancelByZaaerIdAsync(
            int zaaerId,
            PmsCancelPromissoryNoteDto dto,
            CancellationToken cancellationToken = default);
    }
}
