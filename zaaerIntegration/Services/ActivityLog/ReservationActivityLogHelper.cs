using FinanceLedgerAPI.Models;
using zaaerIntegration.Services.ActivityLog;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Services.ActivityLog
{
    public static class ReservationActivityLogHelper
    {
        public static Task LogReservationAsync(
            IReservationActivityLogWriter writer,
            string eventKey,
            Reservation reservation,
            object? payload = null,
            string? iconKey = null,
            int? unitId = null,
            string? refType = null,
            int? refId = null,
            string? refNo = null,
            decimal? amountTo = null,
            CancellationToken cancellationToken = default)
        {
            return writer.LogAsync(
                new ReservationActivityLogEntry
                {
                    EventKey = eventKey,
                    HotelId = reservation.HotelId,
                    ReservationId = reservation.ReservationId,
                    ReservationNo = reservation.ReservationNo,
                    UnitId = unitId,
                    RefType = refType,
                    RefId = refId,
                    RefNo = refNo,
                    AmountTo = amountTo,
                    IconKey = iconKey,
                    Payload = payload ?? new Dictionary<string, object?>
                    {
                        ["reservationNo"] = reservation.ReservationNo
                    },
                    ZaaerId = reservation.ZaaerId
                },
                cancellationToken);
        }
    }
}
