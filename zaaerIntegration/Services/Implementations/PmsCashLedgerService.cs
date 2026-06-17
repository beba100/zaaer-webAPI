using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;
using ExpenseModel = FinanceLedgerAPI.Models.Expense;

namespace zaaerIntegration.Services.Implementations
{
    public sealed class PmsCashLedgerService : IPmsCashLedgerService
    {
        private static readonly HashSet<string> CashInVouchers = new(StringComparer.OrdinalIgnoreCase)
        {
            "receipt",
            "security_deposit"
        };

        private static readonly HashSet<string> CashOutVouchers = new(StringComparer.OrdinalIgnoreCase)
        {
            "refund",
            "security_deposit_refund",
            "transfers_to_bank"
        };

        private readonly ApplicationDbContext _context;
        private readonly ITenantService _tenantService;

        public PmsCashLedgerService(ApplicationDbContext context, ITenantService tenantService)
        {
            _context = context;
            _tenantService = tenantService;
        }

        public async Task BackfillAsync(CancellationToken cancellationToken = default)
        {
            var hotel = await ResolveHotelContextAsync(cancellationToken);
            var hotelIds = ResolveHotelIds(hotel);

            var openings = await _context.CashOpeningBalances
                .AsNoTracking()
                .Where(o => hotelIds.Contains(o.HotelId))
                .OrderBy(o => o.OpeningDate)
                .ToListAsync(cancellationToken);

            foreach (var opening in openings)
            {
                await ReconcileSourceAsync(
                    hotel.HotelZaaerId,
                    sourceType: "OpeningBalance",
                    sourceSubtype: "opening_balance",
                    sourceId: opening.OpeningId,
                    sourceZaaerId: null,
                    sourceNo: $"OB-{opening.OpeningDate:yyyyMMdd}-{opening.OpeningId}",
                    transactionDate: opening.OpeningDate,
                    debit: 0m,
                    credit: Math.Abs(opening.OpeningAmount),
                    movementLabel: "رصيد افتتاحي",
                    description: opening.Notes ?? "رصيد افتتاحي",
                    createdBy: null,
                    shouldPost: opening.OpeningAmount != 0m,
                    cancellationToken);
            }

            var receipts = await _context.PaymentReceipts
                .AsNoTracking()
                .Where(r => hotelIds.Contains(r.HotelId)
                    && r.VoucherCode != null
                    && (CashInVouchers.Contains(r.VoucherCode) || CashOutVouchers.Contains(r.VoucherCode)))
                .OrderBy(r => r.ReceiptDate)
                .ToListAsync(cancellationToken);

            foreach (var receipt in receipts)
            {
                await SyncPaymentReceiptAsync(receipt, cancellationToken);
            }

            var expenses = await _context.Expenses
                .AsNoTracking()
                .Where(e => hotelIds.Contains(e.HotelId))
                .OrderBy(e => e.DateTime)
                .ToListAsync(cancellationToken);

            foreach (var expense in expenses)
            {
                await SyncExpenseAsync(expense, cancellationToken);
            }
        }

        public async Task SyncPaymentReceiptAsync(PaymentReceipt receipt, CancellationToken cancellationToken = default)
        {
            var hotelId = await NormalizeLedgerHotelIdAsync(receipt.HotelId, cancellationToken);
            var voucher = (receipt.VoucherCode ?? receipt.ReceiptType ?? string.Empty).Trim();
            if (!CashInVouchers.Contains(voucher) && !CashOutVouchers.Contains(voucher))
            {
                return;
            }

            var amount = Math.Abs(receipt.AmountPaid);
            var isCancelled = string.Equals(receipt.ReceiptStatus, "cancelled", StringComparison.OrdinalIgnoreCase);
            var isCash = await IsCashReceiptAsync(receipt, cancellationToken);
            var shouldPost = !isCancelled && isCash && amount > 0m;
            var debit = CashOutVouchers.Contains(voucher) ? amount : 0m;
            var credit = CashInVouchers.Contains(voucher) ? amount : 0m;

            await ReconcileSourceAsync(
                hotelId,
                sourceType: "PaymentReceipt",
                sourceSubtype: voucher,
                sourceId: receipt.ReceiptId,
                sourceZaaerId: receipt.ZaaerId,
                sourceNo: receipt.ReceiptNo,
                transactionDate: receipt.ReceiptDate,
                debit: debit,
                credit: credit,
                movementLabel: ResolvePaymentReceiptLabel(voucher),
                description: BuildPaymentReceiptDescription(receipt, voucher),
                createdBy: receipt.CreatedBy,
                shouldPost: shouldPost,
                cancellationToken);
        }

        public async Task SyncExpenseAsync(ExpenseModel expense, CancellationToken cancellationToken = default)
        {
            var hotelId = await NormalizeLedgerHotelIdAsync(expense.HotelId, cancellationToken);
            var isCancelled = string.Equals(expense.ApprovalStatus, "cancelled", StringComparison.OrdinalIgnoreCase);
            var amount = Math.Abs(expense.TotalAmount);

            await ReconcileSourceAsync(
                hotelId,
                sourceType: "Expense",
                sourceSubtype: expense.ApprovalStatus,
                sourceId: expense.OldExpenseId > 0 ? expense.OldExpenseId : expense.ExpenseId,
                sourceZaaerId: expense.ExpenseId,
                sourceNo: expense.ExpenseNo,
                transactionDate: expense.DateTime,
                debit: amount,
                credit: 0m,
                movementLabel: "مصروف",
                description: BuildExpenseDescription(expense),
                createdBy: expense.CreatedBy,
                shouldPost: !isCancelled && amount > 0m,
                cancellationToken);
        }

        public async Task RemoveExpenseEffectAsync(ExpenseModel expense, CancellationToken cancellationToken = default)
        {
            var hotelId = await NormalizeLedgerHotelIdAsync(expense.HotelId, cancellationToken);
            await ReconcileSourceAsync(
                hotelId,
                sourceType: "Expense",
                sourceSubtype: "deleted",
                sourceId: expense.OldExpenseId > 0 ? expense.OldExpenseId : expense.ExpenseId,
                sourceZaaerId: expense.ExpenseId,
                sourceNo: expense.ExpenseNo,
                transactionDate: expense.DateTime,
                debit: 0m,
                credit: 0m,
                movementLabel: "إلغاء أثر مصروف",
                description: $"إلغاء أثر مصروف رقم {expense.ExpenseNo}",
                createdBy: expense.UpdatedBy ?? expense.CreatedBy,
                shouldPost: false,
                cancellationToken);
        }

        public async Task<PmsCashLedgerStatementDto> GetReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            await BackfillAsync(cancellationToken);

            var hotel = await ResolveHotelContextAsync(cancellationToken);
            var from = fromDate.Date;
            var toExclusive = toDate.Date.AddDays(1);

            var opening = await _context.CashLedgerEntries
                .AsNoTracking()
                .Where(e => e.HotelId == hotel.HotelZaaerId && e.TransactionDate < from)
                .SumAsync(e => e.CreditAmount - e.DebitAmount, cancellationToken);

            var entries = await _context.CashLedgerEntries
                .AsNoTracking()
                .Where(e => e.HotelId == hotel.HotelZaaerId
                    && e.TransactionDate >= from
                    && e.TransactionDate < toExclusive)
                .OrderBy(e => e.TransactionDate)
                .ThenBy(e => e.LedgerId)
                .ToListAsync(cancellationToken);

            var cashIn = entries.Sum(e => e.CreditAmount);
            var cashOut = entries.Sum(e => e.DebitAmount);

            var receiptLookup = await BuildPaymentReceiptLookupAsync(entries, cancellationToken);
            var expenseLookup = await BuildExpenseLookupAsync(entries, cancellationToken);

            return new PmsCashLedgerStatementDto
            {
                FromDate = from,
                ToDate = toDate.Date,
                OpeningBalance = opening,
                CashIn = cashIn,
                CashOut = cashOut,
                ClosingBalance = opening + cashIn - cashOut,
                Items = entries.Select(e => MapRow(e, receiptLookup, expenseLookup)).ToList()
            };
        }

        private async Task<IReadOnlyDictionary<long, PaymentReceipt>> BuildPaymentReceiptLookupAsync(
            IReadOnlyList<CashLedgerEntry> entries,
            CancellationToken cancellationToken)
        {
            var receiptIds = entries
                .Where(e => string.Equals(e.SourceType, "PaymentReceipt", StringComparison.OrdinalIgnoreCase)
                    && e.SourceId.HasValue)
                .Select(e => (int)e.SourceId!.Value)
                .Distinct()
                .ToList();

            if (receiptIds.Count == 0)
            {
                return new Dictionary<long, PaymentReceipt>();
            }

            var receipts = await _context.PaymentReceipts
                .AsNoTracking()
                .Where(r => receiptIds.Contains(r.ReceiptId))
                .ToListAsync(cancellationToken);

            return receipts.ToDictionary(r => (long)r.ReceiptId);
        }

        private async Task<IReadOnlyDictionary<long, ExpenseModel>> BuildExpenseLookupAsync(
            IReadOnlyList<CashLedgerEntry> entries,
            CancellationToken cancellationToken)
        {
            var expenseSourceIds = entries
                .Where(e => string.Equals(e.SourceType, "Expense", StringComparison.OrdinalIgnoreCase)
                    && e.SourceId.HasValue)
                .Select(e => e.SourceId!.Value)
                .Distinct()
                .ToList();

            if (expenseSourceIds.Count == 0)
            {
                return new Dictionary<long, ExpenseModel>();
            }

            var expenses = await _context.Expenses
                .AsNoTracking()
                .Where(e => expenseSourceIds.Contains(e.ExpenseId)
                    || expenseSourceIds.Contains(e.OldExpenseId))
                .ToListAsync(cancellationToken);

            var lookup = new Dictionary<long, ExpenseModel>();
            foreach (var expense in expenses)
            {
                lookup[(long)expense.ExpenseId] = expense;
                if (expense.OldExpenseId > 0)
                {
                    lookup[expense.OldExpenseId] = expense;
                }
            }

            return lookup;
        }

        private async Task ReconcileSourceAsync(
            int hotelId,
            string sourceType,
            string? sourceSubtype,
            long? sourceId,
            long? sourceZaaerId,
            string? sourceNo,
            DateTime transactionDate,
            decimal debit,
            decimal credit,
            string movementLabel,
            string? description,
            int? createdBy,
            bool shouldPost,
            CancellationToken cancellationToken)
        {
            var currentNet = await _context.CashLedgerEntries
                .Where(e => e.HotelId == hotelId
                    && e.SourceType == sourceType
                    && e.SourceId == sourceId
                    && e.SourceZaaerId == sourceZaaerId)
                .SumAsync(e => e.CreditAmount - e.DebitAmount, cancellationToken);

            var expectedNet = shouldPost ? credit - debit : 0m;
            var delta = Math.Round(expectedNet - currentNet, 2, MidpointRounding.AwayFromZero);
            if (delta == 0m)
            {
                return;
            }

            var adjustmentDebit = delta < 0m ? Math.Abs(delta) : 0m;
            var adjustmentCredit = delta > 0m ? delta : 0m;
            var idempotencyKey = BuildIdempotencyKey(
                hotelId,
                sourceType,
                sourceId,
                sourceZaaerId,
                Math.Round(currentNet, 2, MidpointRounding.AwayFromZero),
                Math.Round(expectedNet, 2, MidpointRounding.AwayFromZero));

            var exists = await _context.CashLedgerEntries
                .AnyAsync(e => e.IdempotencyKey == idempotencyKey, cancellationToken);
            if (exists)
            {
                return;
            }

            _context.CashLedgerEntries.Add(new CashLedgerEntry
            {
                HotelId = hotelId,
                TransactionDate = transactionDate,
                SourceType = sourceType,
                SourceSubtype = sourceSubtype,
                SourceId = sourceId,
                SourceZaaerId = sourceZaaerId,
                SourceNo = sourceNo,
                MovementLabel = movementLabel,
                DebitAmount = adjustmentDebit,
                CreditAmount = adjustmentCredit,
                Description = description,
                CreatedAt = KsaTime.Now,
                CreatedBy = createdBy,
                Status = shouldPost ? "posted" : "reversal",
                IdempotencyKey = idempotencyKey
            });

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<bool> IsCashReceiptAsync(PaymentReceipt receipt, CancellationToken cancellationToken)
        {
            if (receipt.PaymentMethodId.HasValue && receipt.PaymentMethodId.Value > 0)
            {
                var method = await _context.PaymentMethods
                    .AsNoTracking()
                    .Where(pm => pm.PaymentMethodId == receipt.PaymentMethodId.Value)
                    .Select(pm => new { pm.MethodName, pm.MethodCode })
                    .FirstOrDefaultAsync(cancellationToken);

                if (method != null)
                {
                    return IsCashText(method.MethodCode) || IsCashText(method.MethodName);
                }
            }

            return IsCashText(receipt.PaymentMethod);
        }

        private static bool IsCashText(string? value)
        {
            var text = (value ?? string.Empty).Trim().ToLowerInvariant();
            return text == "cash" || text.Contains("cash") || text.Contains("نقد");
        }

        private static string BuildIdempotencyKey(
            int hotelId,
            string sourceType,
            long? sourceId,
            long? sourceZaaerId,
            decimal currentNet,
            decimal expectedNet) =>
            $"cash-ledger:{hotelId}:{sourceType}:{sourceId?.ToString() ?? "null"}:{sourceZaaerId?.ToString() ?? "null"}:{currentNet:0.00}->{expectedNet:0.00}";

        private static string ResolvePaymentReceiptLabel(string voucher) =>
            voucher switch
            {
                "security_deposit" => "سند قبض تأمين",
                "refund" => "سند صرف إيجار",
                "security_deposit_refund" => "سند صرف تأمين",
                "transfers_to_bank" => "إيداع بنكي",
                _ => "سند قبض إيجار"
            };

        private static string BuildPaymentReceiptDescription(PaymentReceipt receipt, string voucher)
        {
            var receiptNo = (receipt.ReceiptNo ?? string.Empty).Trim();
            var notes = NormalizeDepositNotes(receipt.Notes, voucher);

            if (!string.IsNullOrWhiteSpace(notes))
            {
                return AppendDocumentNo(notes, receiptNo);
            }

            return AppendDocumentNo(ResolvePaymentReceiptLabel(voucher), receiptNo);
        }

        private static string BuildExpenseDescription(ExpenseModel expense)
        {
            var expenseNo = (expense.ExpenseNo ?? string.Empty).Trim();
            var comment = (expense.Comment ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(comment))
            {
                return string.IsNullOrWhiteSpace(expenseNo) ? "مصروف" : $"مصروف {expenseNo}";
            }

            return AppendDocumentNo(comment, expenseNo, separator: " - ");
        }

        private static string NormalizeDepositNotes(string? text, string voucher)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var normalized = text.Trim();
            if (!string.Equals(voucher, "transfers_to_bank", StringComparison.OrdinalIgnoreCase)
                && !normalized.Contains("تحويل بنكي", StringComparison.Ordinal))
            {
                return normalized;
            }

            return normalized
                .Replace("تحويل بنكي", "إيداع بنكي", StringComparison.Ordinal)
                .Replace("تحويل", "إيداع", StringComparison.Ordinal);
        }

        private static string AppendDocumentNo(string text, string documentNo, string separator = " ")
        {
            var body = (text ?? string.Empty).Trim();
            var docNo = (documentNo ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(docNo))
            {
                return body;
            }

            if (!string.IsNullOrWhiteSpace(body)
                && body.Contains(docNo, StringComparison.OrdinalIgnoreCase))
            {
                return body;
            }

            return string.IsNullOrWhiteSpace(body)
                ? docNo
                : $"{body}{separator}{docNo}";
        }

        private static string ResolveReportDescription(
            CashLedgerEntry entry,
            IReadOnlyDictionary<long, PaymentReceipt> receiptLookup,
            IReadOnlyDictionary<long, ExpenseModel> expenseLookup)
        {
            if (entry.SourceId.HasValue)
            {
                if (string.Equals(entry.SourceType, "PaymentReceipt", StringComparison.OrdinalIgnoreCase)
                    && receiptLookup.TryGetValue(entry.SourceId.Value, out var receipt))
                {
                    var voucher = (receipt.VoucherCode ?? receipt.ReceiptType ?? entry.SourceSubtype ?? string.Empty).Trim();
                    return BuildPaymentReceiptDescription(receipt, voucher);
                }

                if (string.Equals(entry.SourceType, "Expense", StringComparison.OrdinalIgnoreCase)
                    && expenseLookup.TryGetValue(entry.SourceId.Value, out var expense))
                {
                    return BuildExpenseDescription(expense);
                }
            }

            return NormalizeLegacyDescription(entry.Description, entry.SourceNo, entry.SourceSubtype);
        }

        private static string NormalizeLegacyDescription(string? description, string? sourceNo, string? sourceSubtype)
        {
            var text = (description ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            var voucher = (sourceSubtype ?? string.Empty).Trim();
            if (string.Equals(voucher, "transfers_to_bank", StringComparison.OrdinalIgnoreCase)
                || text.Contains("تحويل بنكي", StringComparison.Ordinal))
            {
                text = text
                    .Replace("تحويل بنكي", "إيداع بنكي", StringComparison.Ordinal)
                    .Replace("تحويل", "إيداع", StringComparison.Ordinal);
            }

            return AppendDocumentNo(text, sourceNo ?? string.Empty);
        }

        private sealed record HotelContext(int LocalHotelId, int HotelZaaerId);

        private static List<int> ResolveHotelIds(HotelContext hotel) =>
            new[] { hotel.LocalHotelId, hotel.HotelZaaerId }.Distinct().ToList();

        private async Task<HotelContext> ResolveHotelContextAsync(CancellationToken cancellationToken)
        {
            var tenant = _tenantService.GetTenant()
                ?? throw new UnauthorizedAccessException("Tenant not resolved. Provide X-Hotel-Code.");

            var hotelSettings = await _context.HotelSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    h => h.HotelCode != null && h.HotelCode.ToLower() == tenant.Code.ToLower(),
                    cancellationToken)
                ?? throw new InvalidOperationException($"HotelSettings not found for hotel code: {tenant.Code}.");

            if (!hotelSettings.ZaaerId.HasValue)
            {
                throw new InvalidOperationException($"ZaaerId is not configured for hotel code: {tenant.Code}.");
            }

            return new HotelContext(hotelSettings.HotelId, hotelSettings.ZaaerId.Value);
        }

        private async Task<int> NormalizeLedgerHotelIdAsync(int sourceHotelId, CancellationToken cancellationToken)
        {
            var hotel = await ResolveHotelContextAsync(cancellationToken);
            return sourceHotelId == hotel.LocalHotelId || sourceHotelId == hotel.HotelZaaerId
                ? hotel.HotelZaaerId
                : sourceHotelId;
        }

        private static PmsCashLedgerRowDto MapRow(
            CashLedgerEntry entry,
            IReadOnlyDictionary<long, PaymentReceipt> receiptLookup,
            IReadOnlyDictionary<long, ExpenseModel> expenseLookup) => new()
        {
            LedgerId = entry.LedgerId,
            TransactionDate = entry.TransactionDate,
            SourceType = entry.SourceType,
            SourceSubtype = entry.SourceSubtype,
            SourceId = entry.SourceId,
            SourceZaaerId = entry.SourceZaaerId,
            SourceNo = entry.SourceNo,
            MovementLabel = entry.MovementLabel,
            DebitAmount = entry.DebitAmount,
            CreditAmount = entry.CreditAmount,
            BalanceAmount = entry.CreditAmount - entry.DebitAmount,
            Description = ResolveReportDescription(entry, receiptLookup, expenseLookup),
            Status = entry.Status
        };
    }
}
