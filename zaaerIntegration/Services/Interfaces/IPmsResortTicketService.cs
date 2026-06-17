using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Reporting.Abstractions;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IPmsResortTicketService
    {
        Task<IReadOnlyList<PmsResortTicketTypeDto>> ListTicketTypesAsync(string? category = null, CancellationToken cancellationToken = default);
        Task<PmsResortTicketTypeDto?> GetTicketTypeAsync(int id, CancellationToken cancellationToken = default);
        Task<PmsResortTicketTypeDto> CreateTicketTypeAsync(PmsUpsertResortTicketTypeDto dto, CancellationToken cancellationToken = default);
        Task<PmsResortTicketTypeDto?> UpdateTicketTypeAsync(int id, PmsUpsertResortTicketTypeDto dto, CancellationToken cancellationToken = default);
        Task<PmsResortTicketTypeDto?> SetTicketTypeActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default);
        Task<PmsResortTicketLookupsDto> GetLookupsAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PmsResortTicketOrderDto>> ListOrdersAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? reservationId = null,
            string? paymentStatus = null,
            CancellationToken cancellationToken = default);
        Task<PmsResortTicketOrderDto?> GetOrderAsync(int id, CancellationToken cancellationToken = default);
        Task<PmsResortTicketOrderDto> CreateOrderAsync(PmsCreateResortTicketOrderDto dto, CancellationToken cancellationToken = default);
        Task<PmsResortTicketOrderDto?> CancelOrderAsync(int id, PmsCancelResortTicketOrderDto dto, CancellationToken cancellationToken = default);
        Task<ReportRenderResult?> PrintOrderAsync(int id, string paper = "thermal", CancellationToken cancellationToken = default);
        Task<ReportRenderResult?> PrintTicketAsync(int id, string paper = "thermal", CancellationToken cancellationToken = default);
        Task<PmsResortTicketRedeemResultDto> LookupTicketByQrAsync(
            string qrCode,
            string? stationCode = null,
            CancellationToken cancellationToken = default);
        Task<PmsResortTicketRedeemResultDto> RedeemTicketByQrAsync(
            string qrCode,
            string? stationCode = null,
            CancellationToken cancellationToken = default);
        Task<PmsResortTicketBusinessConfigDto> GetBusinessConfigAsync(CancellationToken cancellationToken = default);
        Task<PmsResortTicketBusinessConfigDto> UpdateBusinessConfigAsync(PmsUpsertResortTicketBusinessConfigDto dto, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PmsResortTicketPendingInvoiceOrderDto>> ListPendingInvoiceOrdersAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PmsResortTicketInvoiceListItemDto>> ListTicketInvoicesAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PmsResortTicketInvoiceListItemDto>> CreateInvoicesForOrdersAsync(
            PmsCreateResortTicketInvoicesDto dto,
            CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PmsResortTicketReceiptListItemDto>> ListTicketReceiptsAsync(
            string? receiptKind = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken cancellationToken = default);
        Task<PmsResortTicketFinanceReconciliationDto> GetFinanceReconciliationAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken cancellationToken = default);
    }
}
