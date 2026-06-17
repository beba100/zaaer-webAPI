#pragma warning disable CS1591

using zaaerIntegration.DTOs.Pms;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IPmsDebitNoteService
    {
        Task<IReadOnlyList<PmsAdjustmentRowDto>> ListByInvoiceAsync(
            int invoiceId,
            CancellationToken cancellationToken = default);

        Task<PmsAdjustmentRowDto> CreateAsync(
            PmsCreateDebitNoteDto dto,
            CancellationToken cancellationToken = default);
    }
}
