using FinanceLedgerAPI.Models;
using zaaerIntegration.Services.Implementations;

namespace zaaerIntegration.Utilities
{
    /// <summary>
    /// Hall rent settlement from vouchers (receipt/disbursement) + credit notes — aligned with hall-operations payments popup.
    /// </summary>
    public static class HallEventSettlementHelper
    {
        public const decimal BalanceTolerance = 0.01m;

        public static void AccumulateRentReceipts(
            IEnumerable<PaymentReceipt> receipts,
            out decimal received,
            out decimal disbursedFromReceipts)
        {
            received = 0m;
            disbursedFromReceipts = 0m;
            foreach (var pr in receipts)
            {
                if (!ReservationFinancialSyncService.CountsTowardRentPaymentTotals(pr))
                {
                    continue;
                }

                var amt = Math.Round(Math.Abs(pr.AmountPaid), 2, MidpointRounding.AwayFromZero);
                if (ReservationFinancialSyncService.IsRentReceiptPayment(pr))
                {
                    received += amt;
                }
                else if (ReservationFinancialSyncService.IsRentDisbursementPayment(pr))
                {
                    disbursedFromReceipts += amt;
                }
            }
        }

        public static decimal SumCreditNoteDisbursements(IEnumerable<CreditNote> notes) =>
            notes.Sum(n => Math.Round(n.CreditAmount, 2, MidpointRounding.AwayFromZero));

        public static decimal ComputeBalanceDue(decimal totalAmount, decimal received, decimal disbursed) =>
            Math.Round(totalAmount - received - disbursed, 2, MidpointRounding.AwayFromZero);

        public static bool CanCloseEvent(decimal balanceDue) => balanceDue <= BalanceTolerance;

        public static HashSet<int> BuildReservationLinkKeys(int internalReservationId, int? zaaerId)
        {
            var keys = new HashSet<int> { internalReservationId };
            if (zaaerId is > 0)
            {
                keys.Add(zaaerId.Value);
            }

            return keys;
        }
    }
}
