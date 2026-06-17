using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms.ReservationDetail;

namespace zaaerIntegration.Utilities
{
    /// <summary>
    /// Enforces that every reservation row has a real guest (no DB exceptions).
    /// </summary>
    public static class ReservationGuestGuard
    {
        public const string GuestRequiredKey = "reservationDetail.validation.guestRequired";

        public static async Task<int> RequireResolvedCustomerStorageIdAsync(
            int hotelId,
            int customerRouteId,
            ApplicationDbContext context,
            CancellationToken cancellationToken)
        {
            if (customerRouteId <= 0)
            {
                throw new InvalidOperationException(GuestRequiredKey);
            }

            var customer = await context.Customers.AsNoTracking()
                .FirstOrDefaultAsync(
                    c =>
                        c.HotelId == hotelId
                        && (c.CustomerId == customerRouteId || c.ZaaerId == customerRouteId),
                    cancellationToken);

            if (customer != null && PmsCustomerMarkers.IsDraftPlaceholder(customer))
            {
                throw new InvalidOperationException(GuestRequiredKey);
            }

            if (customer != null)
            {
                return customer.ZaaerId is > 0 ? customer.ZaaerId.Value : customer.CustomerId;
            }

            return customerRouteId;
        }

        public static async Task EnsureGuestInvariantAsync(
            Reservation entity,
            ApplicationDbContext context,
            CancellationToken cancellationToken)
        {
            if (!await HasValidAssignedGuestAsync(entity, context, cancellationToken))
            {
                throw new InvalidOperationException(GuestRequiredKey);
            }
        }

        public static async Task EnsureGuestBeforeOperationalActionAsync(
            Reservation entity,
            ApplicationDbContext context,
            CancellationToken cancellationToken)
        {
            await EnsureGuestInvariantAsync(entity, context, cancellationToken);
        }

        public static async Task EnsureCreatePayloadHasGuestAsync(
            ReservationCreateDto body,
            ApplicationDbContext context,
            int hotelId,
            CancellationToken cancellationToken)
        {
            if (!body.CustomerId.HasValue || body.CustomerId.Value <= 0)
            {
                throw new InvalidOperationException(GuestRequiredKey);
            }

            _ = await RequireResolvedCustomerStorageIdAsync(
                hotelId,
                body.CustomerId.Value,
                context,
                cancellationToken);
        }

        private static async Task<bool> HasValidAssignedGuestAsync(
            Reservation entity,
            ApplicationDbContext context,
            CancellationToken cancellationToken)
        {
            if (!entity.CustomerId.HasValue || entity.CustomerId.Value <= 0)
            {
                return false;
            }

            var customer = await context.Customers.AsNoTracking()
                .FirstOrDefaultAsync(
                    c =>
                        c.HotelId == entity.HotelId
                        && (c.CustomerId == entity.CustomerId.Value || c.ZaaerId == entity.CustomerId.Value),
                    cancellationToken);

            return customer != null && !PmsCustomerMarkers.IsDraftPlaceholder(customer);
        }
    }
}
