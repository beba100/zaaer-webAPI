using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.DTOs.Response;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IPmsPaymentReceiptService
    {
        Task<IReadOnlyList<PmsPaymentReceiptRowDto>> ListByReservationAsync(
            int reservationId,
            string? receiptType = null,
            string? kind = null,
            CancellationToken cancellationToken = default);

        Task<PaymentReceiptResponseDto> CreateAsync(
            PmsCreatePaymentReceiptDto dto,
            CancellationToken cancellationToken = default);

        /// <summary>Update by integration id (<c>payment_receipts.zaaer_id</c>), scoped to hotel + reservation.</summary>
        Task<PaymentReceiptResponseDto> UpdateByZaaerIdAsync(
            int zaaerId,
            PmsUpdatePaymentReceiptDto dto,
            CancellationToken cancellationToken = default);

        Task<PaymentReceiptResponseDto> CancelByZaaerIdAsync(
            int zaaerId,
            PmsCancelPaymentReceiptDto dto,
            CancellationToken cancellationToken = default);

        Task<PmsLastRentReceiptDto?> GetLastRentReceiptAsync(
            int reservationId,
            CancellationToken cancellationToken = default);

        Task<PmsPaymentReceiptRowDto?> GetByZaaerIdAsync(
            int zaaerId,
            CancellationToken cancellationToken = default);
    }
}
