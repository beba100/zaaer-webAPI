using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Integrations
{
    public interface INtmpBookingPayloadBuilder
    {
        Task<NtmpPayloadBuildResult> BuildCreateOrUpdateAsync(
            int reservationId,
            int transactionTypeId,
            string cuFlag,
            bool includeTransactionId,
            CancellationToken cancellationToken = default);
    }

    public sealed class NtmpPayloadBuildResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public NtmpCreateOrUpdateBookingRequest? Request { get; init; }
        public string? GuestName { get; init; }
        public string? UnitNumber { get; init; }
        public string? BookingNo { get; init; }
    }

    public sealed class NtmpBookingPayloadBuilder : INtmpBookingPayloadBuilder
    {
        private readonly ApplicationDbContext _db;
        private readonly NtmpLookupMapper _mapper;

        public NtmpBookingPayloadBuilder(ApplicationDbContext db, NtmpLookupMapper mapper)
        {
            _db = db;
            _mapper = mapper;
        }

        public async Task<NtmpPayloadBuildResult> BuildCreateOrUpdateAsync(
            int reservationId,
            int transactionTypeId,
            string cuFlag,
            bool includeTransactionId,
            CancellationToken cancellationToken = default)
        {
            var reservation = await _db.Reservations
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId, cancellationToken);

            if (reservation == null)
            {
                return Fail("Reservation not found.");
            }

            if (!reservation.CustomerId.HasValue || reservation.CustomerId.Value <= 0)
            {
                return Fail("Reservation has no guest assigned.");
            }

            var customer = await _db.Customers.AsNoTracking()
                .Include(c => c.GuestType)
                .FirstOrDefaultAsync(
                    c =>
                        c.CustomerId == reservation.CustomerId.Value ||
                        c.ZaaerId == reservation.CustomerId.Value,
                    cancellationToken);

            Nationality? nationality = null;
            if (customer?.NId is > 0)
            {
                nationality = await _db.Nationalities.AsNoTracking()
                    .FirstOrDefaultAsync(n => n.NId == customer.NId, cancellationToken);
            }

            var unit = await _db.ReservationUnits.AsNoTracking()
                .Where(u => u.ReservationId == reservation.ReservationId
                            || (reservation.ZaaerId.HasValue && u.ReservationId == reservation.ZaaerId.Value))
                .OrderBy(u => u.CheckInDate)
                .FirstOrDefaultAsync(cancellationToken);

            Apartment? apartment = null;
            RoomType? roomType = null;
            if (unit != null)
            {
                apartment = await _db.Apartments.AsNoTracking()
                    .FirstOrDefaultAsync(
                        a => a.ApartmentId == unit.ApartmentId || a.ZaaerId == unit.ApartmentId,
                        cancellationToken);

                if (apartment?.RoomTypeId is > 0)
                {
                    roomType = await _db.RoomTypes.AsNoTracking()
                        .FirstOrDefaultAsync(rt => rt.RoomTypeId == apartment.RoomTypeId, cancellationToken);
                }
            }

            var checkIn = unit?.CheckInDate ?? reservation.CheckInDate ?? KsaTime.Now.Date;
            var checkOut = unit?.CheckOutDate ?? reservation.CheckOutDate ?? checkIn.AddDays(1);
            var nights = Math.Max(1, (checkOut.Date - checkIn.Date).Days);
            if (nights <= 0)
            {
                nights = 1;
            }

            var total = reservation.TotalAmount ?? 0m;
            var vat = reservation.VatAmount ?? 0m;
            var lodging = reservation.LodgingTaxAmount ?? 0m;
            var discount = reservation.TotalDiscounts ?? 0m;
            var subtotal = reservation.Subtotal ?? Math.Max(0m, total - vat - lodging + discount);
            var daily = nights > 0 ? Math.Round(subtotal / nights, 2, MidpointRounding.AwayFromZero) : subtotal;

            var roomNo = apartment?.ApartmentCode ?? apartment?.ApartmentName ?? unit?.ApartmentId.ToString() ?? "1";
            var channel = NtmpApiConstants.ChannelName;

            var req = new NtmpCreateOrUpdateBookingRequest
            {
                BookingNo = reservation.ReservationNo,
                NationalityCode = _mapper.MapNationalityCode(nationality),
                CheckInDate = NtmpLookupMapper.FormatYmd(checkIn),
                CheckOutDate = NtmpLookupMapper.FormatYmd(checkOut),
                TotalDurationDays = nights.ToString(),
                AllotedRoomNo = roomNo.Length > 50 ? roomNo[..50] : roomNo,
                RoomRentType = _mapper.MapRoomRentType(reservation.RentalType),
                DailyRoomRate = NtmpLookupMapper.FormatAmount(daily),
                TotalRoomRate = NtmpLookupMapper.FormatAmount(subtotal),
                Vat = NtmpLookupMapper.FormatAmount(vat),
                MunicipalityTax = NtmpLookupMapper.FormatAmount(lodging),
                Discount = NtmpLookupMapper.FormatAmount(discount),
                GrandTotal = NtmpLookupMapper.FormatAmount(total),
                TransactionTypeId = transactionTypeId.ToString(),
                Gender = _mapper.MapGender(customer?.Gender),
                TransactionId = includeTransactionId && !string.IsNullOrWhiteSpace(reservation.NtmpTransactionId)
                    ? reservation.NtmpTransactionId
                    : null,
                CheckInTime = transactionTypeId >= NtmpApiConstants.TransactionTypeCheckIn
                    ? NtmpLookupMapper.FormatHhmmss(reservation.CheckInDate ?? KsaTime.Now)
                    : "0",
                CheckOutTime = transactionTypeId >= NtmpApiConstants.TransactionTypeCheckOut
                    ? NtmpLookupMapper.FormatHhmmss(reservation.CheckOutDate ?? reservation.DepartureDate ?? KsaTime.Now)
                    : "0",
                CustomerType = _mapper.MapCustomerType(customer?.GuestType),
                NoOfGuest = "1",
                RoomType = _mapper.MapRoomType(roomType),
                PurposeOfVisit = _mapper.MapPurposeOfVisit(reservation.VisitPurpose),
                DateOfBirth = NtmpLookupMapper.FormatYmd(customer?.BirthdateGregorian ?? customer?.Birthday),
                PaymentType = "1",
                NoOfRooms = "1",
                Channel = channel,
                CuFlag = cuFlag
            };

            if (string.IsNullOrWhiteSpace(req.BookingNo))
            {
                return Fail("Reservation number is required for NTMP.");
            }

            return new NtmpPayloadBuildResult
            {
                Success = true,
                Request = req,
                GuestName = customer?.CustomerName,
                UnitNumber = roomNo,
                BookingNo = reservation.ReservationNo
            };
        }

        private static NtmpPayloadBuildResult Fail(string message) =>
            new() { Success = false, ErrorMessage = message };
    }
}
