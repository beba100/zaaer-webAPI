#pragma warning disable CS1591

using zaaerIntegration.DTOs.Pms.ReservationDetail;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IReservationDetailService
    {
        Task<ReservationDetailDto?> GetByZaaerOrReservationIdAsync(
            int id,
            int? hotelId,
            CancellationToken cancellationToken = default);

        Task<ReservationDetailDto?> PatchReservationAsync(
            int routeId,
            ReservationPmsPatchDto patch,
            int? hotelId,
            CancellationToken cancellationToken = default);

        Task<bool> CheckoutReservationAsync(
            int routeId,
            int? hotelId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancel reservation after verifying no active payment receipts or invoices; frees apartments and room card colors like checkout.
        /// </summary>
        Task<ReservationDetailDto?> CancelReservationAsync(
            int routeId,
            int? hotelId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Check out a single reservation line (unit). When the reservation has only one unit, performs full reservation checkout (same as <see cref="CheckoutReservationAsync"/>).
        /// </summary>
        Task<ReservationDetailDto?> CheckoutReservationUnitAsync(
            int routeId,
            int unitId,
            int? hotelId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Re-open a checked-out reservation for in-house operations (checked-in header, units, rented apartments).
        /// </summary>
        Task<ReservationDetailDto?> ReopenReservationAfterCheckoutAsync(
            int routeId,
            int? hotelId,
            CancellationToken cancellationToken = default);

        Task<ReservationUnitDayRatesResponseDto?> GetUnitDayRatesAsync(
            int routeId,
            int? unitId,
            int? hotelId,
            CancellationToken cancellationToken = default);

        Task<ReservationUnitDayRatesResponseDto?> SaveUnitDayRatesAsync(
            int routeId,
            ReservationUnitDayRatesSaveRequestDto request,
            int? hotelId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a reservation with a required guest and applies the editor payload in one transaction flow.
        /// </summary>
        Task<ReservationDetailDto?> CreateReservationAsync(
            ReservationCreateDto body,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Records a unit swap, updates the reservation line apartment, and returns the refreshed detail.
        /// </summary>
        Task<ReservationDetailDto?> SwapReservationUnitAsync(
            int routeId,
            ReservationUnitSwapRequestDto body,
            int? hotelId,
            int? createdByUserId,
            CancellationToken cancellationToken = default);

        Task<ReservationDiscountApplyResultDto?> ApplyDiscountAsync(
            CreateReservationDiscountDto request,
            CancellationToken cancellationToken = default);

        Task<ReservationDiscountApplyResultDto?> UpdateDiscountAsync(
            int discountId,
            UpdateReservationDiscountDto request,
            CancellationToken cancellationToken = default);

        Task<ReservationDiscountApplyResultDto?> DeleteDiscountAsync(
            int discountId,
            int reservationRouteId,
            int? hotelId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reconciles reservation totals from units, extras, penalties, discounts, and live receipts, then returns the check-out snapshot.
        /// </summary>
        Task<ReservationCheckoutSnapshotDto?> GetCheckoutSnapshotAsync(
            int routeId,
            int? hotelId,
            CancellationToken cancellationToken = default);
    }
}
