using FinanceLedgerAPI.Models;
using zaaerIntegration.Data;

namespace zaaerIntegration.Services.Integrations.Zatca
{
    public interface IZatcaProfileResolver
    {
        /// <summary>
        /// Individual reservation → simplified (reporting).
        /// Corporate reservation (linked company row or type) → standard (clearance).
        /// No reservation → simplified (B2C / POS).
        /// </summary>
        Task<ZatcaProfileResolution> ResolveForInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default);

        Task<ZatcaProfileResolution> ResolveForReservationIdAsync(
            int? reservationRef,
            int? hotelId = null,
            CancellationToken cancellationToken = default);
    }

    public sealed record ZatcaProfileResolution(
        string Profile,
        string SubmissionMode,
        string? ReservationType);

    public sealed class ZatcaProfileResolver : IZatcaProfileResolver
    {
        private readonly ApplicationDbContext _db;

        public ZatcaProfileResolver(ApplicationDbContext db)
        {
            _db = db;
        }

        public Task<ZatcaProfileResolution> ResolveForInvoiceAsync(
            Invoice invoice,
            CancellationToken cancellationToken = default) =>
            ResolveForReservationIdAsync(invoice.ReservationId, invoice.HotelId, cancellationToken);

        public async Task<ZatcaProfileResolution> ResolveForReservationIdAsync(
            int? reservationRef,
            int? hotelId = null,
            CancellationToken cancellationToken = default)
        {
            if (reservationRef is not > 0)
            {
                return Simplified();
            }

            var reservation = await ZatcaReservationLinkage.FindReservationAsync(
                _db,
                reservationRef.Value,
                hotelId,
                cancellationToken);

            if (reservation == null)
            {
                return Simplified();
            }

            CorporateCustomer? corporate = null;
            if (reservation.CorporateId is > 0)
            {
                corporate = await ZatcaReservationLinkage.FindCorporateCustomerAsync(
                    _db,
                    reservation.HotelId,
                    reservation.CorporateId.Value,
                    cancellationToken);
            }

            if (ZatcaReservationLinkage.IsCorporateBooking(reservation, corporate))
            {
                return new ZatcaProfileResolution(
                    ZatcaApiConstants.ProfileStandard,
                    ZatcaApiConstants.ModeClearance,
                    reservation.ReservationType);
            }

            return new ZatcaProfileResolution(
                ZatcaApiConstants.ProfileSimplified,
                ZatcaApiConstants.ModeReporting,
                reservation.ReservationType);
        }

        private static ZatcaProfileResolution Simplified() =>
            new(
                ZatcaApiConstants.ProfileSimplified,
                ZatcaApiConstants.ModeReporting,
                null);
    }
}
