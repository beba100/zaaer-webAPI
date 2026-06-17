using FinanceLedgerAPI.Enums;
using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms.ReservationDetail;

namespace zaaerIntegration.Utilities
{
    public static class ReservationPeriodQueries
    {
        public static async Task<ReservationPeriodListResponseDto> BuildListAsync(
            ApplicationDbContext context,
            Reservation reservation,
            CancellationToken cancellationToken = default)
        {
            var refs = ReservationPeriodStorage.GetReservationStorageRefs(reservation);
            var items = await context.ReservationPeriods.AsNoTracking()
                .Where(p => refs.Contains(p.ReservationId))
                .OrderBy(p => p.FromDate)
                .ThenBy(p => p.PeriodId)
                .ToListAsync(cancellationToken);

            var mapped = items.Select(Map).ToList();
            var rentalTypes = mapped
                .Select(p => p.RentalType)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var active = mapped.LastOrDefault(p =>
                string.Equals(p.Status, ReservationPeriodStatus.Active, StringComparison.OrdinalIgnoreCase));

            return new ReservationPeriodListResponseDto
            {
                ReservationId = ReservationPeriodStorage.GetStorageReservationId(reservation),
                HasMixedRentalPeriods = rentalTypes.Count > 1,
                ActiveRentalType = active?.RentalType,
                Items = mapped
            };
        }

        public static ReservationPeriodDto Map(ReservationPeriod p) =>
            new()
            {
                PeriodId = p.PeriodId,
                ReservationId = p.ReservationId,
                UnitId = p.UnitId,
                RentalType = p.RentalType,
                FromDate = p.FromDate.Date,
                ToDate = p.ToDate.Date,
                GrossRate = p.GrossRate,
                TaxIncluded = p.TaxIncluded,
                Status = p.Status,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            };
    }
}
