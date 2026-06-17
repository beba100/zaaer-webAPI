#pragma warning disable CS1591

using zaaerIntegration.DTOs.Pms;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IPmsInvoiceService
    {
        Task<IReadOnlyList<PmsInvoiceRowDto>> ListByReservationAsync(
            int reservationId,
            CancellationToken cancellationToken = default);

        Task<PmsInvoiceContextDto> GetCreateContextAsync(
            int reservationId,
            CancellationToken cancellationToken = default);

        Task<PmsInvoiceRowDto> CreateAsync(
            PmsCreateInvoiceDto dto,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PmsAdjustmentRowDto>> ListAdjustmentsByInvoiceAsync(
            int invoiceId,
            CancellationToken cancellationToken = default);
    }
}
