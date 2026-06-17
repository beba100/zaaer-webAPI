using FinanceLedgerAPI.Models;

namespace zaaerIntegration.Utilities
{
    /// <summary>
    /// Hall satellite tables and API routes use <c>reservations.zaaer_id</c> when allocated (integration link).
    /// </summary>
    public static class HallReservationLink
    {
        public static int GetStorageId(Reservation reservation) =>
            reservation.ZaaerId is > 0 ? reservation.ZaaerId.Value : reservation.ReservationId;

        public static bool MatchesRouteId(Reservation reservation, int routeId) =>
            reservation.ReservationId == routeId || reservation.ZaaerId == routeId;

        public static bool MatchesStoredReservationId(Reservation reservation, int storedReservationId) =>
            storedReservationId == GetStorageId(reservation) || storedReservationId == reservation.ReservationId;
    }
}
