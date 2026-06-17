using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.Repositories.Interfaces;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Services.Implementations
{
    /// <summary>
    /// Shared reservation financial sync: rent receipts/disbursements → amount_paid / balance_amount.
    /// Promissory notes do not change reservation totals; checkout may treat their face value as coverage.
    /// </summary>
    public sealed class ReservationFinancialSyncService : IReservationFinancialSyncService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUnitOfWork _unitOfWork;

        public ReservationFinancialSyncService(ApplicationDbContext context, IUnitOfWork unitOfWork)
        {
            _context = context;
            _unitOfWork = unitOfWork;
        }

        public async Task SyncReservationRentPaymentTotalsAsync(
            int internalReservationId,
            CancellationToken cancellationToken = default)
        {
            var reservation = await _context.Reservations
                .FirstOrDefaultAsync(r => r.ReservationId == internalReservationId, cancellationToken);

            if (reservation == null)
            {
                return;
            }

            var reservationKeys = BuildReservationKeys(internalReservationId, reservation.ZaaerId);

            var receipts = await _context.PaymentReceipts
                .AsNoTracking()
                .Where(pr => pr.ReservationId.HasValue && reservationKeys.Contains(pr.ReservationId.Value))
                .ToListAsync(cancellationToken);

            decimal amountPaid = 0m;
            foreach (var pr in receipts)
            {
                if (!CountsTowardRentPaymentTotals(pr))
                {
                    continue;
                }

                var amt = Math.Abs(pr.AmountPaid);
                if (IsRentReceiptPayment(pr))
                {
                    amountPaid += amt;
                }
                else
                {
                    amountPaid -= amt;
                }
            }

            var totalAmount = reservation.TotalAmount ?? reservation.Subtotal ?? 0m;
            var balanceAmount = totalAmount - amountPaid;

            if (reservation.AmountPaid == amountPaid && reservation.BalanceAmount == balanceAmount)
            {
                return;
            }

            reservation.AmountPaid = amountPaid;
            reservation.BalanceAmount = balanceAmount;
            await _unitOfWork.SaveChangesAsync();
        }

        internal static List<int> BuildReservationKeys(int internalReservationId, int? zaaerId)
        {
            var keys = new List<int> { internalReservationId };
            if (zaaerId.HasValue && zaaerId.Value > 0)
            {
                keys.Add(zaaerId.Value);
            }

            return keys;
        }

        internal static bool CountsTowardRentPaymentTotals(PaymentReceipt pr)
        {
            if (IsReceiptCancelled(pr))
            {
                return false;
            }

            if (IsPromissoryCollectionReceipt(pr))
            {
                return false;
            }

            var type = (pr.ReceiptType ?? string.Empty).Trim().ToLowerInvariant();
            var voucher = (pr.VoucherCode ?? string.Empty).Trim().ToLowerInvariant();

            if (type is "security_deposit" or "security_deposit_refund")
            {
                return false;
            }

            if (voucher is "security_deposit" or "security_deposit_refund")
            {
                return false;
            }

            return IsRentReceiptPayment(pr) || IsRentDisbursementPayment(pr);
        }

        internal static bool IsPromissoryCollectionReceipt(PaymentReceipt pr)
        {
            var type = (pr.ReceiptType ?? string.Empty).Trim().ToLowerInvariant();
            var voucher = (pr.VoucherCode ?? string.Empty).Trim().ToLowerInvariant();
            return type == "promissory_collection" || voucher == "promissory_collection";
        }

        internal static bool IsReceiptCancelled(PaymentReceipt pr)
        {
            var status = (pr.ReceiptStatus ?? string.Empty).Trim().ToLowerInvariant();
            return status == "cancelled";
        }

        internal static bool IsRentReceiptPayment(PaymentReceipt pr)
        {
            var type = (pr.ReceiptType ?? string.Empty).Trim().ToLowerInvariant();
            var voucher = (pr.VoucherCode ?? string.Empty).Trim().ToLowerInvariant();
            return type == "receipt" || voucher == "receipt";
        }

        internal static bool IsRentDisbursementPayment(PaymentReceipt pr)
        {
            var type = (pr.ReceiptType ?? string.Empty).Trim().ToLowerInvariant();
            var voucher = (pr.VoucherCode ?? string.Empty).Trim().ToLowerInvariant();
            return type is "refund" or "expense" || voucher == "refund" || pr.AmountPaid < 0;
        }

        internal static bool IsPromissoryNoteCancelled(PromissoryNote note)
        {
            var status = (note.Status ?? string.Empty).Trim().ToLowerInvariant();
            return status == "cancelled";
        }

        internal const string ServiceReceiptVoucherCode = "service_receipt";

        internal static bool IsServiceReceiptPayment(PaymentReceipt pr) =>
            pr.OrderId is > 0;

        internal static string ResolveReportVoucherCode(PaymentReceipt pr)
        {
            if (IsServiceReceiptPayment(pr))
            {
                return ServiceReceiptVoucherCode;
            }

            var raw = (pr.VoucherCode ?? pr.ReceiptType ?? string.Empty).Trim().ToLowerInvariant();
            return string.IsNullOrWhiteSpace(raw) ? "receipt" : raw;
        }

        internal static string ResolveReportVoucherLabel(PaymentReceipt pr) =>
            ResolveReportVoucherLabelFromCode(ResolveReportVoucherCode(pr));

        internal static string ResolveReportVoucherLabelFromCode(string voucherCode) =>
            voucherCode switch
            {
                "receipt" => "سند قبض إيجار",
                ServiceReceiptVoucherCode => "سند قبض خدمات",
                "security_deposit" => "سند قبض تأمين",
                "refund" => "سند صرف إيجار",
                "security_deposit_refund" => "سند صرف تأمين",
                _ => voucherCode
            };

        internal static decimal SumActivePromissoryNoteAmounts(IEnumerable<PromissoryNote> notes)
        {
            return notes
                .Where(n => !IsPromissoryNoteCancelled(n))
                .Sum(n => n.Amount);
        }

        /// <summary>
        /// Checkout may proceed when cash balance is cleared or non-cancelled promissory face value covers it.
        /// </summary>
        internal static bool IsBalanceCoveredByPromissoryNotes(decimal balanceAmount, decimal promissoryNotesTotal)
        {
            if (balanceAmount <= 0.01m)
            {
                return true;
            }

            return promissoryNotesTotal >= balanceAmount - 0.01m;
        }

        internal static decimal GetPromissoryOutstanding(PromissoryNote note)
        {
            return Math.Max(0m, note.Amount - note.AmountCollected);
        }
    }
}
