using FinanceLedgerAPI.Models;

namespace zaaerIntegration.Services.Integrations
{
    public interface INtmpGatewayClient
    {
        Task<NtmpGatewayResponse> CreateOrUpdateBookingAsync(
            NtmpDetails settings,
            string password,
            NtmpCreateOrUpdateBookingRequest request,
            CancellationToken cancellationToken = default);

        Task<NtmpGatewayResponse> CancelBookingAsync(
            NtmpDetails settings,
            string password,
            NtmpCancelBookingRequest request,
            CancellationToken cancellationToken = default);

        Task<NtmpGatewayResponse> BookingExpenseAsync(
            NtmpDetails settings,
            string password,
            NtmpBookingExpenseRequest request,
            CancellationToken cancellationToken = default);

        Task<NtmpGatewayResponse> OccupancyUpdateAsync(
            NtmpDetails settings,
            string password,
            NtmpOccupancyUpdateRequest request,
            CancellationToken cancellationToken = default);

        Task<NtmpGatewayResponse> GetTransactionIdByBookingNoAsync(
            NtmpDetails settings,
            string password,
            NtmpGetTransactionIdRequest request,
            CancellationToken cancellationToken = default);
    }
}
