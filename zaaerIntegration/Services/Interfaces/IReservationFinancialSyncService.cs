namespace zaaerIntegration.Services.Interfaces
{
  public interface IReservationFinancialSyncService
  {
    /// <summary>
    /// Recompute <c>reservations.amount_paid</c> and <c>balance_amount</c> from rent receipts and disbursements only.
    /// </summary>
    Task SyncReservationRentPaymentTotalsAsync(
      int internalReservationId,
      CancellationToken cancellationToken = default);
  }
}
