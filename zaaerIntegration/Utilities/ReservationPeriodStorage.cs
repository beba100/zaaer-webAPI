using FinanceLedgerAPI.Models;

namespace zaaerIntegration.Utilities
{
    /// <summary>
    /// Integration storage keys for <c>reservation_periods</c> — aligned with
    /// <c>reservation_unit_day_rates</c> (zaaer reservation id + apartment zaaer id).
    /// </summary>
    public static class ReservationPeriodStorage
    {
        public static int GetStorageReservationId(Reservation reservation) =>
            reservation.ZaaerId is > 0 ? reservation.ZaaerId.Value : reservation.ReservationId;

        public static int GetStorageUnitId(ReservationUnit unit, Apartment? apartment)
        {
            if (apartment != null)
            {
                return apartment.ZaaerId is > 0 ? apartment.ZaaerId.Value : apartment.ApartmentId;
            }

            return unit.ApartmentId;
        }

        public static IReadOnlyList<int> GetReservationStorageRefs(Reservation reservation)
        {
            var storage = GetStorageReservationId(reservation);
            var refs = new HashSet<int> { reservation.ReservationId, storage };
            if (reservation.ZaaerId is > 0)
            {
                refs.Add(reservation.ZaaerId.Value);
            }

            return refs.ToList();
        }

        public static IReadOnlyList<int> GetUnitStorageRefs(ReservationUnit unit, Apartment? apartment)
        {
            var storage = GetStorageUnitId(unit, apartment);
            var refs = new HashSet<int> { unit.UnitId, unit.ApartmentId, storage };
            if (unit.ZaaerId is > 0)
            {
                refs.Add(unit.ZaaerId.Value);
            }

            if (apartment != null)
            {
                refs.Add(apartment.ApartmentId);
                if (apartment.ZaaerId is > 0)
                {
                    refs.Add(apartment.ZaaerId.Value);
                }
            }

            return refs.ToList();
        }

        public static bool PeriodMatchesUnit(ReservationPeriod period, ReservationUnit unit, Apartment? apartment)
        {
            if (!period.UnitId.HasValue)
            {
                return true;
            }

            return GetUnitStorageRefs(unit, apartment).Contains(period.UnitId.Value);
        }
    }
}
