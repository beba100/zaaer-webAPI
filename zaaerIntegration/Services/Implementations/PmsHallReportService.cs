#pragma warning disable CS1591

using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Services.Implementations;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    public sealed class PmsHallReportService : IPmsHallReportService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITenantService _tenantService;
        private readonly IPmsHallEventService _hallEventService;

        public PmsHallReportService(
            ApplicationDbContext context,
            ITenantService tenantService,
            IPmsHallEventService hallEventService)
        {
            _context = context;
            _tenantService = tenantService;
            _hallEventService = hallEventService;
        }

        public async Task<PmsHallBookingsReportDto> GetBookingsReportAsync(
            DateTime fromDate,
            DateTime toDate,
            string? eventStatus = null,
            int? hallId = null,
            CancellationToken cancellationToken = default)
        {
            var items = await _hallEventService.ListEventsAsync(
                fromDate.Date,
                toDate.Date,
                eventStatus,
                hallId,
                cancellationToken: cancellationToken);

            return new PmsHallBookingsReportDto
            {
                Items = items,
                Summary = new PmsHallBookingsReportSummaryDto
                {
                    EventCount = items.Count,
                    TotalRent = Math.Round(items.Sum(i => i.TotalAmount), 2, MidpointRounding.AwayFromZero),
                    TotalDeposit = Math.Round(items.Sum(i => i.DepositAmount), 2, MidpointRounding.AwayFromZero),
                    TotalBalance = Math.Round(items.Sum(i => i.RemainingBalance), 2, MidpointRounding.AwayFromZero)
                }
            };
        }

        public Task<PmsHallFinanceReportDto> GetReceiptsReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default) =>
            BuildPaymentReceiptReportAsync(fromDate, toDate, receiptsOnly: true, cancellationToken);

        public Task<PmsHallFinanceReportDto> GetDisbursementsReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default) =>
            BuildPaymentReceiptReportAsync(fromDate, toDate, receiptsOnly: false, cancellationToken);

        public async Task<PmsHallFinanceReportDto> GetInvoicesReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var metaByKey = await LoadHallEventMetaAsync(scope, cancellationToken);
            if (metaByKey.Count == 0)
            {
                return EmptyFinanceReport();
            }

            var from = fromDate.Date;
            var toExclusive = toDate.Date.AddDays(1);
            var keys = metaByKey.Keys.ToList();

            var invoices = await _context.Invoices.AsNoTracking()
                .Where(i =>
                    (i.HotelId == scope.ScopeHotelId || i.HotelId == scope.LocalHotelId)
                    && i.ReservationId.HasValue
                    && keys.Contains(i.ReservationId.Value)
                    && i.InvoiceDate >= from
                    && i.InvoiceDate < toExclusive)
                .OrderByDescending(i => i.InvoiceDate)
                .ThenByDescending(i => i.InvoiceId)
                .ToListAsync(cancellationToken);

            var rows = invoices
                .Select(inv => MapInvoiceRow(inv, metaByKey))
                .ToList();

            return BuildFinanceReport(rows);
        }

        public async Task<PmsHallFinanceReportDto> GetCreditNotesReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var metaByKey = await LoadHallEventMetaAsync(scope, cancellationToken);
            if (metaByKey.Count == 0)
            {
                return EmptyFinanceReport();
            }

            var from = fromDate.Date;
            var toExclusive = toDate.Date.AddDays(1);
            var keys = metaByKey.Keys.ToList();

            var notesWithInvoices = await _context.CreditNotes.AsNoTracking()
                .Where(cn =>
                    (cn.HotelId == scope.ScopeHotelId || cn.HotelId == scope.LocalHotelId)
                    && cn.ReservationId.HasValue
                    && keys.Contains(cn.ReservationId.Value)
                    && cn.CreditNoteDate >= from
                    && cn.CreditNoteDate < toExclusive)
                .GroupJoin(
                    _context.Invoices.AsNoTracking(),
                    cn => cn.InvoiceId,
                    inv => inv.ZaaerId,
                    (cn, invJoin) => new { cn, invJoin })
                .SelectMany(
                    x => x.invJoin.DefaultIfEmpty(),
                    (x, inv) => new { x.cn, inv })
                .OrderByDescending(x => x.cn.CreditNoteDate)
                .ThenByDescending(x => x.cn.CreditNoteId)
                .ToListAsync(cancellationToken);

            var rows = notesWithInvoices
                .Select(x => MapCreditNoteRow(x.cn, metaByKey, x.inv))
                .ToList();

            return BuildFinanceReport(rows);
        }

        public async Task<PmsDailyJournalReportDto> GetDailyJournalReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var hotelIds = new[] { scope.LocalHotelId, scope.ScopeHotelId }.Distinct().ToList();
            var from = fromDate.Date;
            var toExclusive = toDate.Date.AddDays(1);

            var receipts = await _context.PaymentReceipts.AsNoTracking()
                .Where(pr =>
                    hotelIds.Contains(pr.HotelId)
                    && pr.ReceiptDate >= from
                    && pr.ReceiptDate < toExclusive
                    && (pr.ReceiptStatus == null || pr.ReceiptStatus.ToLower() != "cancelled"))
                .OrderBy(pr => pr.ReceiptDate)
                .ThenBy(pr => pr.ReceiptId)
                .ToListAsync(cancellationToken);

            var journalReceipts = receipts.Where(IsDailyJournalReceipt).ToList();
            if (journalReceipts.Count == 0)
            {
                return EmptyDailyJournalReport();
            }

            var customerLookup = await BuildCustomerLookupAsync(journalReceipts, cancellationToken);
            var reservationLookup = await BuildReservationLookupAsync(journalReceipts, cancellationToken);

            var rows = journalReceipts
                .Select(pr => MapDailyJournalRow(pr, customerLookup, reservationLookup))
                .ToList();

            var receiptTotal = rows
                .Where(r => IsDailyJournalInflow(r.VoucherCode))
                .Sum(r => r.AmountPaid);
            var disbursementTotal = rows
                .Where(r => IsDailyJournalOutflow(r.VoucherCode))
                .Sum(r => r.AmountPaid);

            var voucherBreakdown = rows
                .GroupBy(r => NormalizeDailyJournalVoucherCode(r.VoucherCode))
                .Select(group =>
                {
                    var sample = group.First();
                    return new PmsDailyJournalVoucherSummaryDto
                    {
                        VoucherCode = group.Key,
                        VoucherLabel = sample.VoucherLabel ?? ResolveDailyJournalVoucherLabel(group.Key),
                        Count = group.Count(),
                        TotalAmount = Math.Round(group.Sum(r => r.AmountPaid), 2, MidpointRounding.AwayFromZero)
                    };
                })
                .OrderBy(item => DailyJournalVoucherSortOrder(item.VoucherCode))
                .ToList();

            return new PmsDailyJournalReportDto
            {
                Items = rows,
                Summary = new PmsDailyJournalReportSummaryDto
                {
                    Count = rows.Count,
                    TotalAmount = Math.Round(receiptTotal - disbursementTotal, 2, MidpointRounding.AwayFromZero),
                    VoucherBreakdown = voucherBreakdown
                }
            };
        }

        private static readonly string[] NetworkCashPaymentMethodOrder =
        {
            "Cash", "Mada", "Master Card", "Visa", "Wego", "Rehlat", "Globaleit", "Otherotas", "Agoda", "Expedia"
        };

        public async Task<PmsNetworkCashPaymentsReportDto> GetNetworkCashPaymentsReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var hotelIds = new[] { scope.LocalHotelId, scope.ScopeHotelId }.Distinct().ToList();
            var from = fromDate.Date;
            var toExclusive = toDate.Date.AddDays(1);

            var receipts = await _context.PaymentReceipts.AsNoTracking()
                .Where(pr =>
                    hotelIds.Contains(pr.HotelId)
                    && pr.ReceiptDate >= from
                    && pr.ReceiptDate < toExclusive
                    && (pr.ReceiptStatus == null || pr.ReceiptStatus.ToLower() != "cancelled"))
                .OrderBy(pr => pr.ReceiptDate)
                .ThenBy(pr => pr.ReceiptId)
                .ToListAsync(cancellationToken);

            var filteredReceipts = receipts
                .Where(pr =>
                    IsDailyJournalReceipt(pr)
                    && IsNetworkCashPaymentReceipt(pr)
                    && IsDailyJournalInflow(ReservationFinancialSyncService.ResolveReportVoucherCode(pr)))
                .ToList();

            if (filteredReceipts.Count == 0)
            {
                return EmptyNetworkCashPaymentsReport();
            }

            var customerLookup = await BuildCustomerLookupAsync(filteredReceipts, cancellationToken);
            var reservationLookup = await BuildReservationLookupAsync(filteredReceipts, cancellationToken);

            var rows = filteredReceipts
                .Select(pr => MapDailyJournalRow(pr, customerLookup, reservationLookup))
                .ToList();

            var paymentMethodBreakdown = rows
                .GroupBy(r => NormalizeNetworkCashPaymentMethodKey(r.PaymentMethod))
                .Select(group => new PmsPaymentMethodSummaryDto
                {
                    PaymentMethodKey = group.Key,
                    PaymentMethodLabel = group.Key,
                    Count = group.Count(),
                    TotalAmount = Math.Round(group.Sum(r => r.AmountPaid), 2, MidpointRounding.AwayFromZero)
                })
                .OrderBy(item => NetworkCashPaymentMethodSortOrder(item.PaymentMethodKey))
                .ToList();

            return new PmsNetworkCashPaymentsReportDto
            {
                Items = rows,
                Summary = new PmsNetworkCashPaymentsReportSummaryDto
                {
                    Count = rows.Count,
                    TotalAmount = Math.Round(rows.Sum(r => r.AmountPaid), 2, MidpointRounding.AwayFromZero),
                    PaymentMethodBreakdown = paymentMethodBreakdown
                }
            };
        }

        private static bool IsNetworkCashPaymentReceipt(PaymentReceipt pr) =>
            ResolveNetworkCashPaymentMethodKey(pr.PaymentMethod) != null;

        private static string? ResolveNetworkCashPaymentMethodKey(string? paymentMethod)
        {
            var trimmed = (paymentMethod ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return null;
            }

            foreach (var known in NetworkCashPaymentMethodOrder)
            {
                if (string.Equals(known, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return known;
                }
            }

            var compact = trimmed.ToLowerInvariant().Replace(" ", "");
            return compact switch
            {
                "cash" => "Cash",
                "mada" => "Mada",
                "mastercard" => "Master Card",
                "visa" => "Visa",
                "wego" => "Wego",
                "rehlat" => "Rehlat",
                "globaleit" => "Globaleit",
                "otherotas" => "Otherotas",
                "agoda" => "Agoda",
                "expedia" => "Expedia",
                _ => null
            };
        }

        private static string NormalizeNetworkCashPaymentMethodKey(string? paymentMethod) =>
            ResolveNetworkCashPaymentMethodKey(paymentMethod) ?? string.Empty;

        private static int NetworkCashPaymentMethodSortOrder(string paymentMethodKey)
        {
            for (var i = 0; i < NetworkCashPaymentMethodOrder.Length; i++)
            {
                if (string.Equals(NetworkCashPaymentMethodOrder[i], paymentMethodKey, StringComparison.OrdinalIgnoreCase))
                {
                    return i + 1;
                }
            }

            return 99;
        }

        private static string NormalizeDailyJournalVoucherCode(string? voucherCode) =>
            (voucherCode ?? string.Empty).Trim().ToLowerInvariant();

        private static int DailyJournalVoucherSortOrder(string voucherCode) =>
            voucherCode switch
            {
                "receipt" => 1,
                "service_receipt" => 2,
                "security_deposit" => 3,
                "refund" => 4,
                "security_deposit_refund" => 5,
                _ => 99
            };

        private static string ResolveDailyJournalVoucherLabel(string voucherCode) =>
            ReservationFinancialSyncService.ResolveReportVoucherLabelFromCode(voucherCode);

        private static bool IsDailyJournalInflow(string? voucherCode)
        {
            var code = (voucherCode ?? string.Empty).Trim().ToLowerInvariant();
            return code is "receipt" or "service_receipt" or "security_deposit";
        }

        private static bool IsDailyJournalOutflow(string? voucherCode)
        {
            var code = (voucherCode ?? string.Empty).Trim().ToLowerInvariant();
            return code is "refund" or "security_deposit_refund";
        }

        private static bool IsDailyJournalReceipt(PaymentReceipt pr)
        {
            if (ReservationFinancialSyncService.IsReceiptCancelled(pr))
            {
                return false;
            }

            var voucher = (pr.VoucherCode ?? string.Empty).Trim().ToLowerInvariant();
            if (voucher is "transfers_to_bank"
                or "transfer_bank_balance"
                or "expense"
                or "promissory_collection")
            {
                return false;
            }

            var type = (pr.ReceiptType ?? string.Empty).Trim().ToLowerInvariant();
            var code = !string.IsNullOrWhiteSpace(voucher) ? voucher : type;

            return code is "receipt"
                or "security_deposit"
                or "refund"
                or "security_deposit_refund";
        }

        private static PmsDailyJournalRowDto MapDailyJournalRow(
            PaymentReceipt pr,
            IReadOnlyDictionary<int, Customer> customerLookup,
            IReadOnlyDictionary<int, Reservation> reservationLookup)
        {
            Reservation? reservation = null;
            if (pr.ReservationId.HasValue)
            {
                reservationLookup.TryGetValue(pr.ReservationId.Value, out reservation);
            }

            Customer? customer = null;
            if (pr.CustomerId.HasValue)
            {
                customerLookup.TryGetValue(pr.CustomerId.Value, out customer);
            }

            return new PmsDailyJournalRowDto
            {
                ReceiptId = pr.ReceiptId,
                ReceiptZaaerId = pr.ZaaerId,
                OrderId = pr.OrderId,
                ReceiptNo = pr.ReceiptNo ?? string.Empty,
                ReceiptDate = pr.ReceiptDate,
                CustomerName = customer?.CustomerName,
                ReservationNo = reservation?.ReservationNo,
                ReservationZaaerId = reservation?.ZaaerId,
                ReservationRouteId = reservation != null ? HallReservationLink.GetStorageId(reservation) : 0,
                AmountPaid = Math.Round(Math.Abs(pr.AmountPaid), 2, MidpointRounding.AwayFromZero),
                VoucherCode = ReservationFinancialSyncService.ResolveReportVoucherCode(pr),
                VoucherLabel = ReservationFinancialSyncService.ResolveReportVoucherLabel(pr),
                PaymentMethod = string.IsNullOrWhiteSpace(pr.PaymentMethod) ? null : pr.PaymentMethod.Trim(),
                ReceiptStatus = pr.ReceiptStatus
            };
        }

        private async Task<IReadOnlyDictionary<int, Customer>> BuildCustomerLookupAsync(
            IReadOnlyList<PaymentReceipt> receipts,
            CancellationToken cancellationToken)
        {
            var linkIds = receipts
                .Where(r => r.CustomerId.HasValue)
                .Select(r => r.CustomerId!.Value)
                .Distinct()
                .ToList();

            if (linkIds.Count == 0)
            {
                return new Dictionary<int, Customer>();
            }

            var customers = await _context.Customers.AsNoTracking()
                .Where(c =>
                    (c.ZaaerId.HasValue && linkIds.Contains(c.ZaaerId.Value))
                    || linkIds.Contains(c.CustomerId))
                .ToListAsync(cancellationToken);

            var lookup = new Dictionary<int, Customer>();
            foreach (var customer in customers)
            {
                if (customer.ZaaerId is > 0)
                {
                    lookup[customer.ZaaerId.Value] = customer;
                }

                lookup[customer.CustomerId] = customer;
            }

            return lookup;
        }

        private async Task<IReadOnlyDictionary<int, Reservation>> BuildReservationLookupAsync(
            IReadOnlyList<PaymentReceipt> receipts,
            CancellationToken cancellationToken)
        {
            var linkIds = receipts
                .Where(r => r.ReservationId.HasValue)
                .Select(r => r.ReservationId!.Value)
                .Distinct()
                .ToList();

            if (linkIds.Count == 0)
            {
                return new Dictionary<int, Reservation>();
            }

            var reservations = await _context.Reservations.AsNoTracking()
                .Where(r =>
                    (r.ZaaerId.HasValue && linkIds.Contains(r.ZaaerId.Value))
                    || linkIds.Contains(r.ReservationId))
                .ToListAsync(cancellationToken);

            var lookup = new Dictionary<int, Reservation>();
            foreach (var reservation in reservations)
            {
                if (reservation.ZaaerId is > 0)
                {
                    lookup[reservation.ZaaerId.Value] = reservation;
                }

                lookup[reservation.ReservationId] = reservation;
            }

            return lookup;
        }

        private static PmsDailyJournalReportDto EmptyDailyJournalReport() =>
            new()
            {
                Items = Array.Empty<PmsDailyJournalRowDto>(),
                Summary = new PmsDailyJournalReportSummaryDto()
            };

        private static PmsNetworkCashPaymentsReportDto EmptyNetworkCashPaymentsReport() =>
            new()
            {
                Items = Array.Empty<PmsDailyJournalRowDto>(),
                Summary = new PmsNetworkCashPaymentsReportSummaryDto()
            };

        private async Task<PmsHallFinanceReportDto> BuildPaymentReceiptReportAsync(
            DateTime fromDate,
            DateTime toDate,
            bool receiptsOnly,
            CancellationToken cancellationToken)
        {
            var scope = await GetCurrentHallScopeAsync(cancellationToken);
            var metaByKey = await LoadHallEventMetaAsync(scope, cancellationToken);
            if (metaByKey.Count == 0)
            {
                return EmptyFinanceReport();
            }

            var from = fromDate.Date;
            var toExclusive = toDate.Date.AddDays(1);
            var keys = metaByKey.Keys.ToList();

            var receipts = await _context.PaymentReceipts.AsNoTracking()
                .Where(pr =>
                    pr.ReservationId.HasValue
                    && keys.Contains(pr.ReservationId.Value)
                    && pr.ReceiptDate >= from
                    && pr.ReceiptDate < toExclusive)
                .OrderByDescending(pr => pr.ReceiptDate)
                .ThenByDescending(pr => pr.ReceiptId)
                .ToListAsync(cancellationToken);

            var rows = new List<PmsHallFinanceReportRowDto>();
            foreach (var pr in receipts)
            {
                if (!ReservationFinancialSyncService.CountsTowardRentPaymentTotals(pr))
                {
                    continue;
                }

                var voucher = (pr.VoucherCode ?? string.Empty).Trim().ToLowerInvariant();
                if (voucher == "transfers_to_bank" || voucher == "transfer_bank_balance")
                {
                    continue;
                }

                var isReceipt = ReservationFinancialSyncService.IsRentReceiptPayment(pr);
                var isDisbursement = ReservationFinancialSyncService.IsRentDisbursementPayment(pr);
                if (receiptsOnly && !isReceipt)
                {
                    continue;
                }

                if (!receiptsOnly && !isDisbursement)
                {
                    continue;
                }

                if (!pr.ReservationId.HasValue || !metaByKey.TryGetValue(pr.ReservationId.Value, out var meta))
                {
                    continue;
                }

                rows.Add(MapPaymentReceiptRow(pr, meta, isDisbursement));
            }

            return BuildFinanceReport(rows);
        }

        private static PmsHallFinanceReportRowDto MapPaymentReceiptRow(
            PaymentReceipt pr,
            HallEventMeta meta,
            bool isDisbursement)
        {
            var amount = Math.Round(Math.Abs(pr.AmountPaid), 2, MidpointRounding.AwayFromZero);
            return new PmsHallFinanceReportRowDto
            {
                ReservationRouteId = meta.RouteId,
                ReservationZaaerId = meta.ZaaerId,
                ReservationNo = meta.ReservationNo,
                HallName = meta.HallName,
                CustomerName = meta.CustomerName,
                OccasionName = meta.OccasionName,
                EventDate = meta.EventDate,
                DocumentId = pr.ReceiptId,
                DocumentZaaerId = pr.ZaaerId,
                DocumentNo = pr.ReceiptNo ?? string.Empty,
                DocumentDate = pr.ReceiptDate,
                Amount = amount,
                Status = pr.ReceiptStatus,
                VoucherCode = ReservationFinancialSyncService.ResolveReportVoucherCode(pr),
                VoucherLabel = ReservationFinancialSyncService.ResolveReportVoucherLabel(pr),
                OrderId = pr.OrderId,
                ReceiptType = pr.ReceiptType,
                PaymentMethod = pr.PaymentMethod,
                Reason = pr.Reason,
                Notes = pr.Notes
            };
        }

        private static PmsHallFinanceReportRowDto MapInvoiceRow(
            Invoice inv,
            IReadOnlyDictionary<int, HallEventMeta> metaByKey)
        {
            metaByKey.TryGetValue(inv.ReservationId ?? -1, out var meta);
            return new PmsHallFinanceReportRowDto
            {
                ReservationRouteId = meta?.RouteId ?? 0,
                ReservationZaaerId = meta?.ZaaerId,
                ReservationNo = meta?.ReservationNo ?? string.Empty,
                HallName = meta?.HallName,
                CustomerName = meta?.CustomerName,
                OccasionName = meta?.OccasionName,
                EventDate = meta?.EventDate,
                DocumentId = inv.InvoiceId,
                DocumentZaaerId = inv.ZaaerId,
                DocumentNo = inv.InvoiceNo ?? string.Empty,
                DocumentDate = inv.InvoiceDate,
                Amount = Math.Round(inv.TotalAmount ?? 0m, 2, MidpointRounding.AwayFromZero),
                AmountPaid = Math.Round(inv.AmountPaid, 2, MidpointRounding.AwayFromZero),
                AmountRemaining = Math.Round(inv.AmountRemaining ?? 0m, 2, MidpointRounding.AwayFromZero),
                Status = inv.PaymentStatus,
                Notes = inv.Notes
            };
        }

        private static PmsHallFinanceReportRowDto MapCreditNoteRow(
            CreditNote cn,
            IReadOnlyDictionary<int, HallEventMeta> metaByKey,
            Invoice? linked)
        {
            metaByKey.TryGetValue(cn.ReservationId ?? -1, out var meta);

            return new PmsHallFinanceReportRowDto
            {
                ReservationRouteId = meta?.RouteId ?? 0,
                ReservationZaaerId = meta?.ZaaerId,
                ReservationNo = meta?.ReservationNo ?? string.Empty,
                HallName = meta?.HallName,
                CustomerName = meta?.CustomerName,
                OccasionName = meta?.OccasionName,
                EventDate = meta?.EventDate,
                DocumentId = cn.CreditNoteId,
                DocumentZaaerId = cn.ZaaerId,
                DocumentNo = cn.CreditNoteNo ?? string.Empty,
                DocumentDate = cn.CreditNoteDate,
                Amount = Math.Round(cn.CreditAmount, 2, MidpointRounding.AwayFromZero),
                Status = cn.ZatcaStatus,
                CreditType = cn.CreditType,
                Reason = cn.Reason,
                Notes = cn.Notes,
                LinkedInvoiceId = linked?.InvoiceId,
                LinkedInvoiceZaaerId = linked?.ZaaerId,
                LinkedInvoiceNo = linked?.InvoiceNo
            };
        }

        private async Task<Dictionary<int, HallEventMeta>> LoadHallEventMetaAsync(
            HallScope scope,
            CancellationToken cancellationToken)
        {
            var reservationLinks = _context.Reservations.AsNoTracking()
                .Where(r => r.HotelId == scope.ScopeHotelId || r.HotelId == scope.LocalHotelId)
                .Select(r => new
                {
                    LinkId = r.ZaaerId != null && r.ZaaerId > 0 ? r.ZaaerId.Value : r.ReservationId,
                    Reservation = r
                });

            var customerLinks = _context.Customers.AsNoTracking()
                .Select(c => new
                {
                    LinkId = c.ZaaerId != null && c.ZaaerId > 0 ? c.ZaaerId.Value : c.CustomerId,
                    Customer = c
                });

            var rows = await (
                from profile in _context.ReservationEventProfiles.AsNoTracking()
                where profile.HotelId == scope.ScopeHotelId || profile.HotelId == scope.LocalHotelId
                join resLink in reservationLinks on profile.ReservationId equals resLink.LinkId
                join custLink in customerLinks on resLink.Reservation.CustomerId equals custLink.LinkId into custLinks
                from custLink in custLinks.DefaultIfEmpty()
                join hall in _context.Apartments.AsNoTracking() on profile.HallId equals hall.ApartmentId into halls
                from hall in halls.DefaultIfEmpty()
                select new
                {
                    resLink.Reservation,
                    profile,
                    CustomerName = custLink != null ? custLink.Customer.CustomerName : null,
                    HallName = hall != null ? (hall.ApartmentName ?? hall.ApartmentCode) : null
                }).ToListAsync(cancellationToken);

            var map = new Dictionary<int, HallEventMeta>();
            foreach (var row in rows)
            {
                var meta = new HallEventMeta
                {
                    RouteId = HallReservationLink.GetStorageId(row.Reservation),
                    ZaaerId = row.Reservation.ZaaerId,
                    ReservationNo = row.Reservation.ReservationNo ?? string.Empty,
                    CustomerName = row.CustomerName,
                    HallName = row.HallName,
                    OccasionName = row.profile.OccasionName,
                    EventDate = row.profile.EventDate
                };

                foreach (var key in HallEventSettlementHelper.BuildReservationLinkKeys(
                             row.Reservation.ReservationId,
                             row.Reservation.ZaaerId))
                {
                    map[key] = meta;
                }

                map[row.profile.ReservationId] = meta;
            }

            return map;
        }

        private async Task<HallScope> GetCurrentHallScopeAsync(CancellationToken cancellationToken)
        {
            var tenant = _tenantService.GetTenant()
                ?? throw new UnauthorizedAccessException("Tenant not resolved. Provide X-Hotel-Code.");
            var code = tenant.Code.Trim();
            var hotel = await _context.HotelSettings.AsNoTracking()
                .Where(h => h.HotelCode != null)
                .FirstOrDefaultAsync(h => h.HotelCode!.ToLower() == code.ToLower(), cancellationToken)
                ?? throw new InvalidOperationException($"HotelSettings not found for code: {tenant.Code}");

            var propertyType = hotel.PropertyType?.Trim().ToLowerInvariant() ?? PropertyTypes.Hotel;
            if (!PropertyTypes.IsHall(propertyType))
            {
                throw new InvalidOperationException("Hall reports are available only for hall properties.");
            }

            return new HallScope(hotel.HotelId, hotel.ZaaerId ?? hotel.HotelId);
        }

        private static PmsHallFinanceReportDto BuildFinanceReport(IReadOnlyList<PmsHallFinanceReportRowDto> rows) =>
            new()
            {
                Items = rows,
                Summary = new PmsHallFinanceReportSummaryDto
                {
                    Count = rows.Count,
                    TotalAmount = Math.Round(rows.Sum(r => r.Amount), 2, MidpointRounding.AwayFromZero)
                }
            };

        private static PmsHallFinanceReportDto EmptyFinanceReport() =>
            new()
            {
                Items = Array.Empty<PmsHallFinanceReportRowDto>(),
                Summary = new PmsHallFinanceReportSummaryDto()
            };

        private sealed record HallScope(int LocalHotelId, int ScopeHotelId);

        private sealed class HallEventMeta
        {
            public int RouteId { get; set; }
            public int? ZaaerId { get; set; }
            public string ReservationNo { get; set; } = string.Empty;
            public string? CustomerName { get; set; }
            public string? HallName { get; set; }
            public string? OccasionName { get; set; }
            public DateTime EventDate { get; set; }
        }
    }
}
