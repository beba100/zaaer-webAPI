#pragma warning disable CS1591

using zaaerIntegration.DTOs.Pms;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IPmsCreditNoteService
    {
        Task<IReadOnlyList<PmsAdjustmentRowDto>> ListByInvoiceAsync(
            int invoiceId,
            CancellationToken cancellationToken = default);

        Task<PmsAdjustmentRowDto> CreateAsync(
            PmsCreateCreditNoteDto dto,
            CancellationToken cancellationToken = default);

        Task<int> CountByReservationAsync(
            int reservationId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PmsCreditNoteReservationRowDto>> ListByReservationAsync(
            int reservationId,
            CancellationToken cancellationToken = default);

        Task<PmsCreditNoteReservationRowDto?> GetByZaaerIdAsync(
            int zaaerId,
            CancellationToken cancellationToken = default);
    }
}
