using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.Services.Implementations;

namespace zaaerIntegration.Services.Integrations.Zatca
{
  /// <summary>
  /// PMS linkage: invoice/reservation FKs often store <c>zaaer_id</c>; corporate FK stores
  /// <c>corporate_customers.zaaer_id</c> (see reservations.corporate_id ↔ corporate_customers.zaaer_id).
  /// </summary>
  internal static class ZatcaReservationLinkage
  {
    public static Task<Reservation?> FindReservationAsync(
      ApplicationDbContext db,
      int reservationRef,
      int? hotelId = null,
      CancellationToken cancellationToken = default) =>
      PmsReservationRouteResolver.FindAsync(db, reservationRef, hotelId, asNoTracking: true, cancellationToken);

    /// <param name="corporateRef"><c>reservations.corporate_id</c> — matches <c>corporate_id</c> or <c>zaaer_id</c> on company row.</param>
    public static Task<CorporateCustomer?> FindCorporateCustomerAsync(
      ApplicationDbContext db,
      int hotelId,
      int corporateRef,
      CancellationToken cancellationToken = default) =>
      db.CorporateCustomers.AsNoTracking()
        .FirstOrDefaultAsync(
          c => c.HotelId == hotelId
               && (c.CorporateId == corporateRef || c.ZaaerId == corporateRef),
          cancellationToken);

    public static bool IsCorporateBooking(Reservation reservation, CorporateCustomer? corporateCustomer)
    {
      if (corporateCustomer != null)
      {
        return true;
      }

      if (reservation.CorporateId is > 0)
      {
        return true;
      }

      var type = reservation.ReservationType?.Trim();
      if (string.IsNullOrEmpty(type))
      {
        return false;
      }

      return type.Equals("corporate", StringComparison.OrdinalIgnoreCase)
             || type.Equals("company", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ZATCA credit/debit profile: <c>standard</c> (B2B clearance) vs <c>simplified</c> (B2C reporting).
    /// </summary>
    public static async Task<string> ResolveCreditNoteTypeAsync(
      ApplicationDbContext db,
      int? reservationRef,
      int? hotelId = null,
      CancellationToken cancellationToken = default)
    {
      if (reservationRef is not > 0)
      {
        return "simplified";
      }

      var reservation = await FindReservationAsync(db, reservationRef.Value, hotelId, cancellationToken);
      if (reservation == null)
      {
        return "simplified";
      }

      CorporateCustomer? corporate = null;
      if (reservation.CorporateId is > 0)
      {
        corporate = await FindCorporateCustomerAsync(
          db,
          reservation.HotelId,
          reservation.CorporateId.Value,
          cancellationToken);
      }

      return IsCorporateBooking(reservation, corporate) ? "standard" : "simplified";
    }
  }
}
