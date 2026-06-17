using zaaerIntegration.DTOs.Pms;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IPmsPosOrderService
    {
        Task<PmsPosOrderDto> CreateOrderAsync(PmsCreatePosOrderDto dto, CancellationToken cancellationToken = default);
        Task<PmsPosOrderDto?> GetOrderAsync(int orderId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PmsPosOrderDto>> ListRecentOrdersAsync(int? outletId, int take, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PmsPosOrderListItemDto>> ListOrdersAsync(int? outletId, int take, CancellationToken cancellationToken = default);
        Task<PmsPosOrderDto> UpdateOrderReceiptAsync(int orderId, PmsUpdatePosOrderReceiptDto dto, CancellationToken cancellationToken = default);
        Task<PmsPosOrderDto> CancelOrderAsync(int orderId, CancellationToken cancellationToken = default);

        Task<PmsPosOrderDto> UpdateTransferredOrderAsync(
            int orderId,
            PmsUpdateTransferredPosOrderDto dto,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks transferred POS orders as cancelled when their matching extra line is removed from a reservation folio.
        /// Does not remove <c>reservation_extras</c> rows — the caller replaces extras in the same transaction.
        /// </summary>
        Task CancelTransferredOrdersForRemovedExtrasAsync(
            IReadOnlyList<string> orderNos,
            int reservationRouteId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PmsPosInHouseReservationDto>> ListInHouseReservationsAsync(
            CancellationToken cancellationToken = default);
    }
}
