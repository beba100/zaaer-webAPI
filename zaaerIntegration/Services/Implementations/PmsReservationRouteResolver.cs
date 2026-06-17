using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;

namespace zaaerIntegration.Services.Implementations
{
    /// <summary>
    /// PMS routes (e.g. <c>reservation-detail.html?id=</c>) use Zaaer integration id when set; fall back to internal <c>reservation_id</c>.
    /// </summary>
    internal static class PmsReservationRouteResolver
    {
        internal static async Task<Reservation?> FindAsync(
            ApplicationDbContext context,
            int routeId,
            int? hotelId = null,
            bool asNoTracking = true,
            CancellationToken cancellationToken = default)
        {
            var query = context.Reservations.AsQueryable();
            if (asNoTracking)
            {
                query = query.AsNoTracking();
            }

            if (hotelId.HasValue)
            {
                query = query.Where(r => r.HotelId == hotelId.Value);
            }

            return await query
                .Where(r => r.ZaaerId == routeId || r.ReservationId == routeId)
                .OrderByDescending(r => r.ZaaerId == routeId ? 1 : 0)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
