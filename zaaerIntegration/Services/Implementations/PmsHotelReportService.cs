#pragma warning disable CS1591

using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using zaaerIntegration.Configuration;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    public sealed class PmsHotelReportService : IPmsHotelReportService
    {
        private readonly ApplicationDbContext _context;
        private readonly MasterDbContext _masterContext;
        private readonly ITenantService _tenantService;
        private readonly PaymentDailyNetExTaxOptions _netExTaxOptions;

        public PmsHotelReportService(
            ApplicationDbContext context,
            MasterDbContext masterContext,
            ITenantService tenantService,
            IOptions<PaymentDailyNetExTaxOptions> netExTaxOptions)
        {
            _context = context;
            _masterContext = masterContext;
            _tenantService = tenantService;
            _netExTaxOptions = netExTaxOptions.Value;
        }

        public async Task<PmsHotelBookingsReportDto> GetBookingsReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var hallLinkIds = await GetHallEventReservationLinkIdsAsync(scope, cancellationToken);
            var from = fromDate.Date;
            var toExclusive = toDate.Date.AddDays(1);

            var reservations = (await _context.Reservations.AsNoTracking()
                    .Where(r =>
                        (r.HotelId == scope.ScopeHotelId || r.HotelId == scope.LocalHotelId)
                        && r.ReservationDate >= from
                        && r.ReservationDate < toExclusive)
                    .OrderByDescending(r => r.ReservationDate)
                    .ThenByDescending(r => r.ReservationId)
                    .ToListAsync(cancellationToken))
                .Where(r => IsHotelRoomReservation(r, hallLinkIds))
                .ToList();

            if (reservations.Count == 0)
            {
                return EmptyBookingsReport();
            }

            var customerLinkIds = reservations
                .Where(r => r.CustomerId.HasValue)
                .Select(r => r.CustomerId!.Value)
                .Distinct()
                .ToList();

            var customers = customerLinkIds.Count == 0
                ? new List<Customer>()
                : await _context.Customers.AsNoTracking()
                    .Where(c =>
                        (c.ZaaerId.HasValue && customerLinkIds.Contains(c.ZaaerId.Value))
                        || customerLinkIds.Contains(c.CustomerId))
                    .ToListAsync(cancellationToken);

            var customerLookup = BuildCustomerLookup(customers);

            var guestCategoryIds = customers
                .Where(c => c.GuestCategoryId.HasValue)
                .Select(c => c.GuestCategoryId!.Value)
                .Distinct()
                .ToList();

            var guestCategories = guestCategoryIds.Count == 0
                ? new List<GuestCategory>()
                : await _context.GuestCategories.AsNoTracking()
                    .Where(gc => guestCategoryIds.Contains(gc.GcId))
                    .ToListAsync(cancellationToken);

            var guestCategoryLookup = guestCategories.ToDictionary(gc => gc.GcId);

            var corporateLinkIds = reservations
                .Where(r => r.CorporateId.HasValue)
                .Select(r => r.CorporateId!.Value)
                .Distinct()
                .ToList();

            var corporates = corporateLinkIds.Count == 0
                ? new List<CorporateCustomer>()
                : await _context.CorporateCustomers.AsNoTracking()
                    .Where(c =>
                        (c.ZaaerId.HasValue && corporateLinkIds.Contains(c.ZaaerId.Value))
                        || corporateLinkIds.Contains(c.CorporateId))
                    .ToListAsync(cancellationToken);

            var corporateLookup = BuildCorporateLookup(corporates);

            var reservationLinkIds = reservations
                .Select(HallReservationLink.GetStorageId)
                .Distinct()
                .ToList();

            var unitLabels = await BuildUnitLabelsByReservationLinkIdAsync(reservationLinkIds, cancellationToken);
            var paymentAggregates = await BuildPaymentAggregatesByReservationAsync(reservations, cancellationToken);

            var rows = reservations.Select(r =>
            {
                var linkId = HallReservationLink.GetStorageId(r);
                Customer? customer = null;
                if (r.CustomerId.HasValue)
                {
                    customerLookup.TryGetValue(r.CustomerId.Value, out customer);
                }

                GuestCategory? guestCategory = null;
                if (customer?.GuestCategoryId is int gcId)
                {
                    guestCategoryLookup.TryGetValue(gcId, out guestCategory);
                }

                CorporateCustomer? corporate = null;
                if (r.CorporateId.HasValue)
                {
                    corporateLookup.TryGetValue(r.CorporateId.Value, out corporate);
                }

                paymentAggregates.TryGetValue(linkId, out var payments);
                unitLabels.TryGetValue(linkId, out var unitLabel);

                return new PmsHotelBookingsReportRowDto
                {
                    ReservationId = r.ReservationId,
                    ReservationRouteId = linkId,
                    ReservationZaaerId = r.ZaaerId,
                    ReservationNo = r.ReservationNo ?? string.Empty,
                    Classifier = ResolveClassifier(
                        r.CmBookingNo,
                        r.ExternalRefNo,
                        guestCategory?.GcNameAr,
                        guestCategory?.GcName),
                    Source = string.IsNullOrWhiteSpace(r.Source) ? null : r.Source.Trim(),
                    CustomerName = customer?.CustomerName,
                    CompanyName = corporate?.CorporateName,
                    UnitLabel = string.IsNullOrWhiteSpace(unitLabel) ? null : unitLabel,
                    Status = r.Status,
                    RentalType = r.RentalType,
                    CheckInDate = r.CheckInDate,
                    CheckOutDate = r.CheckOutDate,
                    ReservationDate = r.ReservationDate,
                    CreatedAt = r.CreatedAt,
                    TotalExtra = Math.Round(r.TotalExtra ?? 0m, 2, MidpointRounding.AwayFromZero),
                    TotalTax = Math.Round(r.TotalTaxAmount ?? 0m, 2, MidpointRounding.AwayFromZero),
                    TotalAmount = Math.Round(r.TotalAmount ?? r.Subtotal ?? 0m, 2, MidpointRounding.AwayFromZero),
                    SecurityDeposit = payments.SecurityDeposit,
                    AmountPaid = Math.Round(r.AmountPaid ?? 0m, 2, MidpointRounding.AwayFromZero),
                    Refunded = payments.Refunded,
                    BalanceAmount = Math.Round(r.BalanceAmount ?? 0m, 2, MidpointRounding.AwayFromZero)
                };
            }).ToList();

            return new PmsHotelBookingsReportDto
            {
                Items = rows,
                Summary = new PmsHotelBookingsReportSummaryDto
                {
                    Count = rows.Count,
                    TotalAmount = Math.Round(rows.Sum(r => r.TotalAmount), 2, MidpointRounding.AwayFromZero),
                    TotalPaid = Math.Round(rows.Sum(r => r.AmountPaid), 2, MidpointRounding.AwayFromZero),
                    TotalBalance = Math.Round(rows.Sum(r => r.BalanceAmount), 2, MidpointRounding.AwayFromZero),
                    TotalRefunded = Math.Round(rows.Sum(r => r.Refunded), 2, MidpointRounding.AwayFromZero),
                    TotalSecurityDeposit = Math.Round(rows.Sum(r => r.SecurityDeposit), 2, MidpointRounding.AwayFromZero)
                }
            };
        }

        public Task<PmsHotelFinanceReportDto> GetReceiptsReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default) =>
            BuildPaymentReceiptReportAsync(fromDate, toDate, receiptsOnly: true, cancellationToken);

        public Task<PmsHotelFinanceReportDto> GetDisbursementsReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default) =>
            BuildPaymentReceiptReportAsync(fromDate, toDate, receiptsOnly: false, cancellationToken);

        public async Task<PmsHotelFinanceReportDto> GetInvoicesReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var from = fromDate.Date;
            var toExclusive = toDate.Date.AddDays(1);

            var invoices = await _context.Invoices.AsNoTracking()
                .Where(i =>
                    (i.HotelId == scope.ScopeHotelId || i.HotelId == scope.LocalHotelId)
                    && i.ReservationId.HasValue
                    && i.InvoiceDate >= from
                    && i.InvoiceDate < toExclusive)
                .OrderByDescending(i => i.InvoiceDate)
                .ThenByDescending(i => i.InvoiceId)
                .ToListAsync(cancellationToken);

            var metaByKey = await LoadHotelReservationMetaAsync(
                scope,
                invoices.Where(i => i.ReservationId.HasValue).Select(i => i.ReservationId!.Value).ToList(),
                cancellationToken);

            var rows = invoices
                .Where(inv => inv.ReservationId.HasValue && metaByKey.ContainsKey(inv.ReservationId.Value))
                .Select(inv => MapInvoiceRow(inv, metaByKey))
                .ToList();

            return BuildFinanceReport(rows);
        }

        public async Task<PmsHotelFinanceReportDto> GetCreditNotesReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var from = fromDate.Date;
            var toExclusive = toDate.Date.AddDays(1);

            var notesWithInvoices = await _context.CreditNotes.AsNoTracking()
                .Where(cn =>
                    (cn.HotelId == scope.ScopeHotelId || cn.HotelId == scope.LocalHotelId)
                    && cn.ReservationId.HasValue
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

            var metaByKey = await LoadHotelReservationMetaAsync(
                scope,
                notesWithInvoices.Where(x => x.cn.ReservationId.HasValue).Select(x => x.cn.ReservationId!.Value).ToList(),
                cancellationToken);

            var rows = notesWithInvoices
                .Where(x => x.cn.ReservationId.HasValue && metaByKey.ContainsKey(x.cn.ReservationId.Value))
                .Select(x => MapCreditNoteRow(x.cn, metaByKey, x.inv))
                .ToList();

            return BuildFinanceReport(rows);
        }

        public async Task<PmsDailyJournalReportDto> GetDailyJournalReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
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
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
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

        public async Task<PmsHotelDeparturesReportDto> GetDeparturesReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var hallLinkIds = await GetHallEventReservationLinkIdsAsync(scope, cancellationToken);
            var from = fromDate.Date;
            var toExclusive = toDate.Date.AddDays(1);

            var reservations = (await _context.Reservations.AsNoTracking()
                    .Where(r => r.HotelId == scope.ScopeHotelId || r.HotelId == scope.LocalHotelId)
                    .ToListAsync(cancellationToken))
                .Where(r => IsHotelRoomReservation(r, hallLinkIds))
                .Where(r => !IsCancelledReservation(r))
                .ToList();

            if (reservations.Count == 0)
            {
                return EmptyDeparturesReport();
            }

            var reservationLinkIds = reservations
                .Select(HallReservationLink.GetStorageId)
                .Distinct()
                .ToList();

            var units = await _context.ReservationUnits.AsNoTracking()
                .Where(u => reservationLinkIds.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);

            var unitsByLinkId = units
                .GroupBy(u => u.ReservationId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<ReservationUnit>)g.ToList());

            var customerLinkIds = reservations
                .Where(r => r.CustomerId.HasValue)
                .Select(r => r.CustomerId!.Value)
                .Distinct()
                .ToList();

            var customers = customerLinkIds.Count == 0
                ? new List<Customer>()
                : await _context.Customers.AsNoTracking()
                    .Where(c =>
                        (c.ZaaerId.HasValue && customerLinkIds.Contains(c.ZaaerId.Value))
                        || customerLinkIds.Contains(c.CustomerId))
                    .ToListAsync(cancellationToken);

            var customerLookup = BuildCustomerLookup(customers);
            var unitLabels = await BuildUnitLabelsByReservationLinkIdAsync(reservationLinkIds, cancellationToken);

            var rows = new List<PmsHotelDeparturesReportRowDto>();
            foreach (var reservation in reservations)
            {
                var linkId = HallReservationLink.GetStorageId(reservation);
                if (!unitsByLinkId.TryGetValue(linkId, out var resUnits)
                    && reservation.ZaaerId is > 0)
                {
                    unitsByLinkId.TryGetValue(reservation.ZaaerId.Value, out resUnits);
                }

                resUnits ??= Array.Empty<ReservationUnit>();
                var departureDate = ResolveReservationDepartureDate(reservation, resUnits);
                if (departureDate < from || departureDate >= toExclusive)
                {
                    continue;
                }

                Customer? customer = null;
                if (reservation.CustomerId.HasValue)
                {
                    customerLookup.TryGetValue(reservation.CustomerId.Value, out customer);
                }

                unitLabels.TryGetValue(linkId, out var unitLabel);
                if (string.IsNullOrWhiteSpace(unitLabel) && reservation.ZaaerId is > 0)
                {
                    unitLabels.TryGetValue(reservation.ZaaerId.Value, out unitLabel);
                }

                rows.Add(new PmsHotelDeparturesReportRowDto
                {
                    ReservationId = reservation.ReservationId,
                    ReservationRouteId = linkId,
                    ReservationZaaerId = reservation.ZaaerId,
                    ReservationNo = reservation.ReservationNo ?? string.Empty,
                    DepartureDate = departureDate,
                    UnitLabel = string.IsNullOrWhiteSpace(unitLabel) ? null : unitLabel,
                    UnitRentAmount = Math.Round(
                        resUnits.Sum(u => u.TotalAmount),
                        2,
                        MidpointRounding.AwayFromZero),
                    RentalType = string.IsNullOrWhiteSpace(reservation.RentalType)
                        ? null
                        : reservation.RentalType.Trim(),
                    CustomerName = customer?.CustomerName,
                    MobileNo = customer?.MobileNo
                });
            }

            rows = rows
                .OrderBy(r => r.DepartureDate)
                .ThenBy(r => r.UnitLabel)
                .ThenBy(r => r.ReservationNo)
                .ToList();

            return new PmsHotelDeparturesReportDto
            {
                Items = rows,
                Summary = new PmsHotelDeparturesReportSummaryDto
                {
                    Count = rows.Count
                }
            };
        }

        public async Task<PmsHotelOnlineBookingsReportDto> GetOnlineBookingsReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var hallLinkIds = await GetHallEventReservationLinkIdsAsync(scope, cancellationToken);
            var from = fromDate.Date;
            var toExclusive = toDate.Date.AddDays(1);

            var reservations = (await _context.Reservations.AsNoTracking()
                    .Where(r =>
                        (r.HotelId == scope.ScopeHotelId || r.HotelId == scope.LocalHotelId)
                        && r.ReservationDate >= from
                        && r.ReservationDate < toExclusive)
                    .OrderByDescending(r => r.ReservationDate)
                    .ThenByDescending(r => r.ReservationId)
                    .ToListAsync(cancellationToken))
                .Where(r => IsHotelRoomReservation(r, hallLinkIds))
                .Where(r => !IsCancelledReservation(r))
                .Where(r => !IsReceptionSource(r.Source))
                .ToList();

            if (reservations.Count == 0)
            {
                return EmptyOnlineBookingsReport();
            }

            var customerLinkIds = reservations
                .Where(r => r.CustomerId.HasValue)
                .Select(r => r.CustomerId!.Value)
                .Distinct()
                .ToList();

            var customers = customerLinkIds.Count == 0
                ? new List<Customer>()
                : await _context.Customers.AsNoTracking()
                    .Where(c =>
                        (c.ZaaerId.HasValue && customerLinkIds.Contains(c.ZaaerId.Value))
                        || customerLinkIds.Contains(c.CustomerId))
                    .ToListAsync(cancellationToken);

            var customerLookup = BuildCustomerLookup(customers);

            var rows = reservations.Select(r =>
            {
                Customer? customer = null;
                if (r.CustomerId.HasValue)
                {
                    customerLookup.TryGetValue(r.CustomerId.Value, out customer);
                }

                return new PmsHotelOnlineBookingsReportRowDto
                {
                    ReservationId = r.ReservationId,
                    ReservationRouteId = HallReservationLink.GetStorageId(r),
                    ReservationZaaerId = r.ZaaerId,
                    ReservationNo = r.ReservationNo ?? string.Empty,
                    ReservationDate = r.ReservationDate,
                    Source = NormalizeOnlineBookingSourceLabel(r.Source),
                    CustomerName = customer?.CustomerName,
                    CustomerId = r.CustomerId,
                    TotalAmount = Math.Round(r.TotalAmount ?? r.Subtotal ?? 0m, 2, MidpointRounding.AwayFromZero)
                };
            }).ToList();

            var sourceBreakdown = rows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Source) ? "—" : r.Source!.Trim())
                .Select(group => new PmsHotelOnlineBookingsSourceSummaryDto
                {
                    Source = group.Key,
                    Count = group.Count(),
                    TotalAmount = Math.Round(group.Sum(r => r.TotalAmount), 2, MidpointRounding.AwayFromZero)
                })
                .OrderByDescending(item => item.TotalAmount)
                .ThenBy(item => item.Source)
                .ToList();

            return new PmsHotelOnlineBookingsReportDto
            {
                Items = rows,
                Summary = new PmsHotelOnlineBookingsReportSummaryDto
                {
                    Count = rows.Count,
                    TotalAmount = Math.Round(rows.Sum(r => r.TotalAmount), 2, MidpointRounding.AwayFromZero),
                    SourceBreakdown = sourceBreakdown
                }
            };
        }

        public async Task<PmsHotelUnitTransfersReportDto> GetUnitTransfersReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var hallLinkIds = await GetHallEventReservationLinkIdsAsync(scope, cancellationToken);
            var from = fromDate.Date;
            var toExclusive = toDate.Date.AddDays(1);

            var swapReservationLinkIds = await _context.ReservationUnitSwaps.AsNoTracking()
                .Where(s => s.CreatedAt >= from && s.CreatedAt < toExclusive)
                .Select(s => s.ReservationId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (swapReservationLinkIds.Count == 0)
            {
                return EmptyUnitTransfersReport();
            }

            var reservations = (await _context.Reservations.AsNoTracking()
                    .Where(r =>
                        (r.HotelId == scope.ScopeHotelId || r.HotelId == scope.LocalHotelId)
                        && (swapReservationLinkIds.Contains(r.ReservationId)
                            || (r.ZaaerId.HasValue && swapReservationLinkIds.Contains(r.ZaaerId.Value))))
                    .ToListAsync(cancellationToken))
                .Where(r => IsHotelRoomReservation(r, hallLinkIds))
                .ToList();

            if (reservations.Count == 0)
            {
                return EmptyUnitTransfersReport();
            }

            var reservationLookup = BuildReservationLinkLookup(reservations);
            var matchedReservationKeys = reservationLookup.Keys.ToList();

            var matchedSwaps = await _context.ReservationUnitSwaps.AsNoTracking()
                .Where(s =>
                    s.CreatedAt >= from
                    && s.CreatedAt < toExclusive
                    && matchedReservationKeys.Contains(s.ReservationId))
                .OrderByDescending(s => s.CreatedAt)
                .ThenByDescending(s => s.SwitchId)
                .ToListAsync(cancellationToken);

            if (matchedSwaps.Count == 0)
            {
                return EmptyUnitTransfersReport();
            }

            var customerLinkIds = matchedSwaps
                .Select(s => reservationLookup[s.ReservationId].CustomerId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var customers = customerLinkIds.Count == 0
                ? new List<Customer>()
                : await _context.Customers.AsNoTracking()
                    .Where(c =>
                        (c.ZaaerId.HasValue && customerLinkIds.Contains(c.ZaaerId.Value))
                        || customerLinkIds.Contains(c.CustomerId))
                    .ToListAsync(cancellationToken);

            var customerLookup = BuildCustomerLookup(customers);

            var apartmentLinkIds = matchedSwaps
                .SelectMany(s => new[] { s.FromApartmentId, s.ToApartmentId })
                .Distinct()
                .ToList();

            var apartments = await _context.Apartments.AsNoTracking()
                .Where(a =>
                    (a.HotelId == scope.ScopeHotelId || a.HotelId == scope.LocalHotelId)
                    && (apartmentLinkIds.Contains(a.ApartmentId)
                        || (a.ZaaerId.HasValue && apartmentLinkIds.Contains(a.ZaaerId.Value))))
                .ToListAsync(cancellationToken);

            var roomTypeLinkIds = apartments
                .Where(a => a.RoomTypeId.HasValue)
                .Select(a => a.RoomTypeId!.Value)
                .Distinct()
                .ToList();

            var roomTypes = roomTypeLinkIds.Count == 0
                ? new List<RoomType>()
                : await _context.RoomTypes.AsNoTracking()
                    .Where(rt =>
                        (rt.HotelId == scope.ScopeHotelId || rt.HotelId == scope.LocalHotelId)
                        && (roomTypeLinkIds.Contains(rt.RoomTypeId)
                            || (rt.ZaaerId.HasValue && roomTypeLinkIds.Contains(rt.ZaaerId.Value))))
                    .ToListAsync(cancellationToken);

            var roomTypeLookup = BuildRoomTypeLinkLookup(roomTypes);
            var apartmentInfoLookup = BuildApartmentInfoLookup(apartments, roomTypeLookup);

            var userIds = matchedSwaps
                .Where(s => s.CreatedByUserId is > 0)
                .Select(s => s.CreatedByUserId!.Value)
                .Distinct()
                .ToList();

            var users = userIds.Count == 0
                ? new List<MasterRbacUser>()
                : await _masterContext.RbacUsers.AsNoTracking()
                    .Where(u => userIds.Contains(u.UserId))
                    .ToListAsync(cancellationToken);

            var userLookup = users.ToDictionary(u => u.UserId, FormatPmsUserDisplayName);

            var rows = matchedSwaps.Select(swap =>
            {
                var reservation = reservationLookup[swap.ReservationId];
                Customer? customer = null;
                if (reservation.CustomerId.HasValue)
                {
                    customerLookup.TryGetValue(reservation.CustomerId.Value, out customer);
                }

                apartmentInfoLookup.TryGetValue(swap.FromApartmentId, out var fromInfo);
                apartmentInfoLookup.TryGetValue(swap.ToApartmentId, out var toInfo);

                string? createdByUserName = null;
                if (swap.CreatedByUserId is > 0
                    && userLookup.TryGetValue(swap.CreatedByUserId.Value, out var userName))
                {
                    createdByUserName = userName;
                }

                return new PmsHotelUnitTransfersReportRowDto
                {
                    SwitchId = swap.SwitchId,
                    ReservationId = swap.ReservationId,
                    ReservationRouteId = HallReservationLink.GetStorageId(reservation),
                    ReservationZaaerId = reservation.ZaaerId,
                    ReservationNo = reservation.ReservationNo ?? string.Empty,
                    UnitId = swap.UnitId,
                    FromApartmentId = swap.FromApartmentId,
                    ToApartmentId = swap.ToApartmentId,
                    FromUnitLabel = fromInfo?.Label,
                    FromRoomTypeName = fromInfo?.RoomTypeName,
                    ToUnitLabel = toInfo?.Label,
                    ToRoomTypeName = toInfo?.RoomTypeName,
                    ApplyMode = string.IsNullOrWhiteSpace(swap.ApplyMode) ? null : swap.ApplyMode.Trim(),
                    EffectiveDate = swap.EffectiveDate?.Date,
                    Comment = string.IsNullOrWhiteSpace(swap.Comment) ? null : swap.Comment.Trim(),
                    CreatedAt = swap.CreatedAt,
                    CustomerName = customer?.CustomerName,
                    CreatedByUserName = createdByUserName
                };
            }).ToList();

            return new PmsHotelUnitTransfersReportDto
            {
                Items = rows,
                Summary = new PmsHotelUnitTransfersReportSummaryDto
                {
                    Count = rows.Count
                }
            };
        }

        public async Task<PmsHotelMonthEndClosingReportDto> GetMonthEndClosingReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            var tenant = _tenantService.GetTenant()
                ?? throw new UnauthorizedAccessException("Tenant not resolved. Provide X-Hotel-Code.");
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var hotelIds = new[] { scope.LocalHotelId, scope.ScopeHotelId }.Distinct().ToList();
            var from = fromDate.Date;
            var to = toDate.Date;
            var toExclusive = to.AddDays(1);

            var receipts = await _context.PaymentReceipts.AsNoTracking()
                .Where(pr =>
                    hotelIds.Contains(pr.HotelId)
                    && pr.ReceiptDate >= from
                    && pr.ReceiptDate < toExclusive
                    && pr.ReceiptStatus != null
                    && pr.ReceiptStatus.ToLower() == "paid")
                .ToListAsync(cancellationToken);

            var expenses = await _context.Expenses.AsNoTracking()
                .Where(e =>
                    hotelIds.Contains(e.HotelId)
                    && e.DateTime >= from
                    && e.DateTime < toExclusive
                    && e.ApprovalStatus != null
                    && e.ApprovalStatus.ToLower() != "cancelled")
                .ToListAsync(cancellationToken);

            var usesVatOnly = PaymentDailyNetExTaxHelper.RowUsesVatOnlyNetExTax(
                tenant.Code,
                tenant.Id,
                scope.ScopeHotelId,
                _netExTaxOptions);

            var dayMap = new Dictionary<DateTime, MonthClosingDayAccumulator>();
            for (var day = from; day <= to; day = day.AddDays(1))
            {
                dayMap[day] = new MonthClosingDayAccumulator();
            }

            foreach (var pr in receipts)
            {
                var day = pr.ReceiptDate.Date;
                if (!dayMap.TryGetValue(day, out var bucket))
                {
                    continue;
                }

                if (IsMonthClosingBankDeposit(pr))
                {
                    bucket.DepositsAmount += pr.AmountPaid;
                    continue;
                }

                if (!IsMonthClosingOperationalReceipt(pr))
                {
                    continue;
                }

                bucket.OperationalGrossNet += pr.AmountPaid;
                switch (ResolveMonthClosingPaymentBucket(pr.PaymentMethod))
                {
                    case MonthClosingPaymentBucket.Cash:
                        bucket.CashAmount += pr.AmountPaid;
                        break;
                    case MonthClosingPaymentBucket.Mada:
                        bucket.MadaAmount += pr.AmountPaid;
                        break;
                    case MonthClosingPaymentBucket.BankTransfer:
                        bucket.BankTransferAmount += pr.AmountPaid;
                        break;
                    default:
                        bucket.OtherPaidAmount += pr.AmountPaid;
                        break;
                }

                if (IsDailyJournalReceipt(pr))
                {
                    bucket.RentInsuranceNet += pr.AmountPaid;
                }
            }

            foreach (var expense in expenses)
            {
                var day = expense.DateTime.Date;
                if (!dayMap.TryGetValue(day, out var bucket))
                {
                    continue;
                }

                bucket.ExpensesAmount -= Math.Abs(expense.TotalAmount);
            }

            var rows = new List<PmsHotelMonthEndClosingReportRowDto>();
            foreach (var day in dayMap.Keys.OrderBy(d => d))
            {
                var bucket = dayMap[day];
                rows.Add(new PmsHotelMonthEndClosingReportRowDto
                {
                    Date = day,
                    RentInsuranceNet = RoundMoney(bucket.RentInsuranceNet),
                    CashAmount = RoundMoney(bucket.CashAmount),
                    MadaAmount = RoundMoney(bucket.MadaAmount),
                    OtherPaidAmount = RoundMoney(bucket.OtherPaidAmount),
                    BankTransferAmount = RoundMoney(bucket.BankTransferAmount),
                    NetExTax = PaymentDailyNetExTaxHelper.CalcNetExTax(bucket.OperationalGrossNet, usesVatOnly),
                    DepositsAmount = RoundMoney(bucket.DepositsAmount),
                    ExpensesAmount = RoundMoney(bucket.ExpensesAmount)
                });
            }

            return new PmsHotelMonthEndClosingReportDto
            {
                Items = rows,
                Summary = new PmsHotelMonthEndClosingReportSummaryDto
                {
                    DayCount = rows.Count,
                    RentInsuranceNet = RoundMoney(rows.Sum(r => r.RentInsuranceNet)),
                    CashAmount = RoundMoney(rows.Sum(r => r.CashAmount)),
                    MadaAmount = RoundMoney(rows.Sum(r => r.MadaAmount)),
                    OtherPaidAmount = RoundMoney(rows.Sum(r => r.OtherPaidAmount)),
                    BankTransferAmount = RoundMoney(rows.Sum(r => r.BankTransferAmount)),
                    NetExTax = RoundMoney(rows.Sum(r => r.NetExTax)),
                    DepositsAmount = RoundMoney(rows.Sum(r => r.DepositsAmount)),
                    ExpensesAmount = RoundMoney(rows.Sum(r => r.ExpensesAmount))
                }
            };
        }

        private sealed class MonthClosingDayAccumulator
        {
            public decimal RentInsuranceNet { get; set; }
            public decimal CashAmount { get; set; }
            public decimal MadaAmount { get; set; }
            public decimal OtherPaidAmount { get; set; }
            public decimal BankTransferAmount { get; set; }
            public decimal OperationalGrossNet { get; set; }
            public decimal DepositsAmount { get; set; }
            public decimal ExpensesAmount { get; set; }
        }

        private enum MonthClosingPaymentBucket
        {
            Cash,
            Mada,
            BankTransfer,
            OtherPaid
        }

        private static bool IsMonthClosingOperationalReceipt(PaymentReceipt pr)
        {
            var voucher = (pr.VoucherCode ?? string.Empty).Trim().ToLowerInvariant();
            return voucher is not ("transfers_to_bank" or "transfer_bank_balance");
        }

        private static bool IsMonthClosingBankDeposit(PaymentReceipt pr)
        {
            var voucher = (pr.VoucherCode ?? string.Empty).Trim().ToLowerInvariant();
            if (voucher is not ("transfers_to_bank" or "transfer_bank_balance"))
            {
                return false;
            }

            var bankName = (pr.BankName ?? string.Empty).Trim().ToLowerInvariant();
            return bankName != "expense";
        }

        private static MonthClosingPaymentBucket ResolveMonthClosingPaymentBucket(string? paymentMethod)
        {
            var normalized = ResolveNetworkCashPaymentMethodKey(paymentMethod);
            if (string.Equals(normalized, "Cash", StringComparison.OrdinalIgnoreCase))
            {
                return MonthClosingPaymentBucket.Cash;
            }

            if (string.Equals(normalized, "Mada", StringComparison.OrdinalIgnoreCase))
            {
                return MonthClosingPaymentBucket.Mada;
            }

            var trimmed = (paymentMethod ?? string.Empty).Trim();
            if (string.Equals(trimmed, "Bank Transfer", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed.Replace(" ", ""), "BankTransfer", StringComparison.OrdinalIgnoreCase))
            {
                return MonthClosingPaymentBucket.BankTransfer;
            }

            return MonthClosingPaymentBucket.OtherPaid;
        }

        private static decimal RoundMoney(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private async Task<HotelScope> GetCurrentHotelScopeAsync(CancellationToken cancellationToken)
        {
            var tenant = _tenantService.GetTenant()
                ?? throw new UnauthorizedAccessException("Tenant not resolved. Provide X-Hotel-Code.");
            var code = tenant.Code.Trim();
            var hotel = await _context.HotelSettings.AsNoTracking()
                .Where(h => h.HotelCode != null)
                .FirstOrDefaultAsync(h => h.HotelCode!.ToLower() == code.ToLower(), cancellationToken)
                ?? throw new InvalidOperationException($"HotelSettings not found for code: {tenant.Code}");

            var propertyType = hotel.PropertyType?.Trim().ToLowerInvariant() ?? PropertyTypes.Hotel;
            if (PropertyTypes.IsHall(propertyType))
            {
                throw new InvalidOperationException("Hotel reports are available only for hotel and resort properties.");
            }

            if (!PropertyTypes.IsHotel(propertyType) && !PropertyTypes.IsResort(propertyType))
            {
                throw new InvalidOperationException("Hotel reports are available only for hotel and resort properties.");
            }

            return new HotelScope(hotel.HotelId, hotel.ZaaerId ?? hotel.HotelId);
        }

        private async Task<HashSet<int>> GetHallEventReservationLinkIdsAsync(
            HotelScope scope,
            CancellationToken cancellationToken)
        {
            var ids = await _context.ReservationEventProfiles.AsNoTracking()
                .Where(p => p.HotelId == scope.ScopeHotelId || p.HotelId == scope.LocalHotelId)
                .Select(p => p.ReservationId)
                .Distinct()
                .ToListAsync(cancellationToken);

            return ids.ToHashSet();
        }

        private static bool IsHotelRoomReservation(Reservation r, HashSet<int> hallLinkIds)
        {
            if (hallLinkIds.Contains(r.ReservationId))
            {
                return false;
            }

            if (hallLinkIds.Contains(HallReservationLink.GetStorageId(r)))
            {
                return false;
            }

            return true;
        }

        private async Task<Dictionary<int, HotelReservationMeta>> LoadHotelReservationMetaAsync(
            HotelScope scope,
            IReadOnlyCollection<int> reservationKeys,
            CancellationToken cancellationToken)
        {
            if (reservationKeys.Count == 0)
            {
                return new Dictionary<int, HotelReservationMeta>();
            }

            var distinctKeys = reservationKeys
                .Where(key => key > 0)
                .Distinct()
                .ToList();
            if (distinctKeys.Count == 0)
            {
                return new Dictionary<int, HotelReservationMeta>();
            }

            var hallLinkIds = await GetHallEventReservationLinkIdsAsync(scope, cancellationToken);

            var reservations = (await _context.Reservations.AsNoTracking()
                    .Where(r =>
                        (r.HotelId == scope.ScopeHotelId || r.HotelId == scope.LocalHotelId)
                        && (distinctKeys.Contains(r.ReservationId) ||
                            (r.ZaaerId.HasValue && distinctKeys.Contains(r.ZaaerId.Value))))
                    .ToListAsync(cancellationToken))
                .Where(r => IsHotelRoomReservation(r, hallLinkIds))
                .ToList();

            if (reservations.Count == 0)
            {
                return new Dictionary<int, HotelReservationMeta>();
            }

            var customerLinkIds = reservations
                .Where(r => r.CustomerId.HasValue)
                .Select(r => r.CustomerId!.Value)
                .Distinct()
                .ToList();

            var customers = customerLinkIds.Count == 0
                ? new List<Customer>()
                : await _context.Customers.AsNoTracking()
                    .Where(c =>
                        (c.ZaaerId.HasValue && customerLinkIds.Contains(c.ZaaerId.Value))
                        || customerLinkIds.Contains(c.CustomerId))
                    .ToListAsync(cancellationToken);

            var customerLookup = BuildCustomerLookup(customers);

            var reservationLinkIds = reservations
                .Select(HallReservationLink.GetStorageId)
                .Distinct()
                .ToList();

            var unitLabels = await BuildUnitLabelsByReservationLinkIdAsync(reservationLinkIds, cancellationToken);

            var map = new Dictionary<int, HotelReservationMeta>();
            foreach (var reservation in reservations)
            {
                Customer? customer = null;
                if (reservation.CustomerId.HasValue)
                {
                    customerLookup.TryGetValue(reservation.CustomerId.Value, out customer);
                }

                var linkId = HallReservationLink.GetStorageId(reservation);
                unitLabels.TryGetValue(linkId, out var unitLabel);

                var meta = new HotelReservationMeta
                {
                    RouteId = linkId,
                    ZaaerId = reservation.ZaaerId,
                    ReservationNo = reservation.ReservationNo ?? string.Empty,
                    CustomerName = customer?.CustomerName,
                    UnitLabel = string.IsNullOrWhiteSpace(unitLabel) ? null : unitLabel
                };

                foreach (var key in HallEventSettlementHelper.BuildReservationLinkKeys(
                             reservation.ReservationId,
                             reservation.ZaaerId))
                {
                    map[key] = meta;
                }
            }

            return map;
        }

        private async Task<PmsHotelFinanceReportDto> BuildPaymentReceiptReportAsync(
            DateTime fromDate,
            DateTime toDate,
            bool receiptsOnly,
            CancellationToken cancellationToken)
        {
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var from = fromDate.Date;
            var toExclusive = toDate.Date.AddDays(1);

            var receipts = await _context.PaymentReceipts.AsNoTracking()
                .Where(pr =>
                    pr.ReservationId.HasValue
                    && (pr.HotelId == scope.ScopeHotelId || pr.HotelId == scope.LocalHotelId)
                    && pr.ReceiptDate >= from
                    && pr.ReceiptDate < toExclusive)
                .OrderByDescending(pr => pr.ReceiptDate)
                .ThenByDescending(pr => pr.ReceiptId)
                .ToListAsync(cancellationToken);

            receipts = receipts
                .Where(pr =>
                {
                    if (!ReservationFinancialSyncService.CountsTowardRentPaymentTotals(pr))
                    {
                        return false;
                    }

                    var voucher = (pr.VoucherCode ?? string.Empty).Trim().ToLowerInvariant();
                    if (voucher is "transfers_to_bank" or "transfer_bank_balance")
                    {
                        return false;
                    }

                    var isReceipt = ReservationFinancialSyncService.IsRentReceiptPayment(pr);
                    var isDisbursement = ReservationFinancialSyncService.IsRentDisbursementPayment(pr);
                    return receiptsOnly ? isReceipt : isDisbursement;
                })
                .ToList();

            var metaByKey = await LoadHotelReservationMetaAsync(
                scope,
                receipts.Where(pr => pr.ReservationId.HasValue).Select(pr => pr.ReservationId!.Value).ToList(),
                cancellationToken);

            var rows = new List<PmsHotelFinanceReportRowDto>();
            foreach (var pr in receipts)
            {
                if (!pr.ReservationId.HasValue || !metaByKey.TryGetValue(pr.ReservationId.Value, out var meta))
                {
                    continue;
                }

                rows.Add(MapPaymentReceiptRow(pr, meta));
            }

            return BuildFinanceReport(rows);
        }

        private async Task<Dictionary<int, (decimal SecurityDeposit, decimal Refunded)>> BuildPaymentAggregatesByReservationAsync(
            IReadOnlyList<Reservation> reservations,
            CancellationToken cancellationToken)
        {
            var allKeys = new HashSet<int>();
            foreach (var reservation in reservations)
            {
                foreach (var key in HallEventSettlementHelper.BuildReservationLinkKeys(
                             reservation.ReservationId,
                             reservation.ZaaerId))
                {
                    allKeys.Add(key);
                }
            }

            if (allKeys.Count == 0)
            {
                return new Dictionary<int, (decimal SecurityDeposit, decimal Refunded)>();
            }

            var receipts = await _context.PaymentReceipts.AsNoTracking()
                .Where(pr => pr.ReservationId.HasValue && allKeys.Contains(pr.ReservationId.Value))
                .ToListAsync(cancellationToken);

            var result = new Dictionary<int, (decimal SecurityDeposit, decimal Refunded)>();
            foreach (var reservation in reservations)
            {
                var linkId = HallReservationLink.GetStorageId(reservation);
                var keys = HallEventSettlementHelper.BuildReservationLinkKeys(
                    reservation.ReservationId,
                    reservation.ZaaerId);

                var related = receipts.Where(pr =>
                    pr.ReservationId.HasValue && keys.Contains(pr.ReservationId.Value));

                AccumulatePaymentTotals(related, out var securityDeposit, out var refunded);
                result[linkId] = (securityDeposit, refunded);
            }

            return result;
        }

        private static void AccumulatePaymentTotals(
            IEnumerable<PaymentReceipt> receipts,
            out decimal securityDeposit,
            out decimal refunded)
        {
            securityDeposit = 0m;
            refunded = 0m;

            foreach (var pr in receipts)
            {
                if (ReservationFinancialSyncService.IsReceiptCancelled(pr))
                {
                    continue;
                }

                var voucher = (pr.VoucherCode ?? string.Empty).Trim().ToLowerInvariant();
                var type = (pr.ReceiptType ?? string.Empty).Trim().ToLowerInvariant();
                var code = !string.IsNullOrWhiteSpace(voucher) ? voucher : type;
                var amount = Math.Round(Math.Abs(pr.AmountPaid), 2, MidpointRounding.AwayFromZero);

                if (code is "security_deposit" || type is "security_deposit")
                {
                    securityDeposit += amount;
                }
                else if (code is "security_deposit_refund" || type is "security_deposit_refund")
                {
                    securityDeposit -= amount;
                }
                else if (ReservationFinancialSyncService.IsRentDisbursementPayment(pr)
                         && (code is "refund" || type is "refund"))
                {
                    refunded += amount;
                }
            }

            securityDeposit = Math.Round(securityDeposit, 2, MidpointRounding.AwayFromZero);
            refunded = Math.Round(refunded, 2, MidpointRounding.AwayFromZero);
        }

        private async Task<Dictionary<int, string>> BuildUnitLabelsByReservationLinkIdAsync(
            IReadOnlyList<int> reservationLinkIds,
            CancellationToken cancellationToken)
        {
            if (reservationLinkIds.Count == 0)
            {
                return new Dictionary<int, string>();
            }

            var units = await _context.ReservationUnits.AsNoTracking()
                .Where(u => reservationLinkIds.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);

            if (units.Count == 0)
            {
                return new Dictionary<int, string>();
            }

            var apartmentLinkIds = units
                .Select(u => u.ApartmentId)
                .Distinct()
                .ToList();

            var apartments = await _context.Apartments.AsNoTracking()
                .Where(a =>
                    apartmentLinkIds.Contains(a.ApartmentId)
                    || (a.ZaaerId.HasValue && apartmentLinkIds.Contains(a.ZaaerId.Value)))
                .ToListAsync(cancellationToken);

            var apartmentLookup = new Dictionary<int, Apartment>();
            foreach (var apartment in apartments)
            {
                if (apartment.ZaaerId is > 0)
                {
                    apartmentLookup[apartment.ZaaerId.Value] = apartment;
                }

                apartmentLookup[apartment.ApartmentId] = apartment;
            }

            var result = new Dictionary<int, string>();
            foreach (var group in units.GroupBy(u => u.ReservationId))
            {
                var labels = group
                    .Select(u => apartmentLookup.TryGetValue(u.ApartmentId, out var apartment) ? apartment : null)
                    .Where(apartment => apartment != null)
                    .Select(apartment =>
                        !string.IsNullOrWhiteSpace(apartment!.ApartmentCode)
                            ? apartment.ApartmentCode
                            : apartment.ApartmentName)
                    .Where(label => !string.IsNullOrWhiteSpace(label))
                    .Distinct()
                    .ToList();

                result[group.Key] = labels.Count == 0 ? string.Empty : string.Join(", ", labels);
            }

            return result;
        }

        private static Dictionary<int, Reservation> BuildReservationLinkLookup(IEnumerable<Reservation> reservations)
        {
            var lookup = new Dictionary<int, Reservation>();
            foreach (var reservation in reservations)
            {
                lookup[HallReservationLink.GetStorageId(reservation)] = reservation;
                lookup[reservation.ReservationId] = reservation;
                if (reservation.ZaaerId is > 0)
                {
                    lookup[reservation.ZaaerId.Value] = reservation;
                }
            }

            return lookup;
        }

        private sealed record ApartmentReportInfo(string? Label, string? RoomTypeName);

        private static Dictionary<int, RoomType> BuildRoomTypeLinkLookup(IEnumerable<RoomType> roomTypes)
        {
            var lookup = new Dictionary<int, RoomType>();
            foreach (var roomType in roomTypes)
            {
                lookup[roomType.RoomTypeId] = roomType;
                if (roomType.ZaaerId is > 0)
                {
                    lookup[roomType.ZaaerId.Value] = roomType;
                }
            }

            return lookup;
        }

        private static Dictionary<int, ApartmentReportInfo> BuildApartmentInfoLookup(
            IEnumerable<Apartment> apartments,
            IReadOnlyDictionary<int, RoomType> roomTypeLookup)
        {
            var apartmentByLink = new Dictionary<int, Apartment>();
            foreach (var apartment in apartments)
            {
                apartmentByLink[apartment.ApartmentId] = apartment;
                if (apartment.ZaaerId is > 0)
                {
                    apartmentByLink[apartment.ZaaerId.Value] = apartment;
                }
            }

            var result = new Dictionary<int, ApartmentReportInfo>();
            foreach (var pair in apartmentByLink)
            {
                var apartment = pair.Value;
                var label = !string.IsNullOrWhiteSpace(apartment.ApartmentCode)
                    ? apartment.ApartmentCode.Trim()
                    : apartment.ApartmentName?.Trim();

                string? roomTypeName = null;
                if (apartment.RoomTypeId is int roomTypeId
                    && roomTypeId > 0
                    && roomTypeLookup.TryGetValue(roomTypeId, out var roomType))
                {
                    roomTypeName = string.IsNullOrWhiteSpace(roomType.RoomTypeName)
                        ? null
                        : roomType.RoomTypeName.Trim();
                }

                result[pair.Key] = new ApartmentReportInfo(label, roomTypeName);
            }

            return result;
        }

        private static string FormatPmsUserDisplayName(MasterRbacUser user)
        {
            var name = $"{user.FirstName} {user.LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return string.IsNullOrWhiteSpace(user.Username) ? string.Empty : user.Username.Trim();
        }

        private static Dictionary<int, string> BuildApartmentLabelLookup(IEnumerable<Apartment> apartments)
        {
            var apartmentLookup = new Dictionary<int, Apartment>();
            foreach (var apartment in apartments)
            {
                if (apartment.ZaaerId is > 0)
                {
                    apartmentLookup[apartment.ZaaerId.Value] = apartment;
                }

                apartmentLookup[apartment.ApartmentId] = apartment;
            }

            var labels = new Dictionary<int, string>();
            foreach (var pair in apartmentLookup)
            {
                var apartment = pair.Value;
                var label = !string.IsNullOrWhiteSpace(apartment.ApartmentCode)
                    ? apartment.ApartmentCode.Trim()
                    : apartment.ApartmentName?.Trim();
                if (!string.IsNullOrWhiteSpace(label))
                {
                    labels[pair.Key] = label!;
                }
            }

            return labels;
        }

        private static string? ResolveApartmentLabel(IReadOnlyDictionary<int, string> lookup, int linkId) =>
            lookup.TryGetValue(linkId, out var label) ? label : null;

        private static Dictionary<int, Customer> BuildCustomerLookup(IEnumerable<Customer> customers)
        {
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

        private static Dictionary<int, CorporateCustomer> BuildCorporateLookup(IEnumerable<CorporateCustomer> corporates)
        {
            var lookup = new Dictionary<int, CorporateCustomer>();
            foreach (var corporate in corporates)
            {
                if (corporate.ZaaerId is > 0)
                {
                    lookup[corporate.ZaaerId.Value] = corporate;
                }

                lookup[corporate.CorporateId] = corporate;
            }

            return lookup;
        }

        private static string? ResolveClassifier(
            string? cmBookingNo,
            int? externalRefNo,
            string? gcNameAr,
            string? gcName)
        {
            if (!string.IsNullOrWhiteSpace(cmBookingNo))
            {
                return cmBookingNo.Trim();
            }

            if (externalRefNo.HasValue)
            {
                return externalRefNo.Value.ToString();
            }

            if (!string.IsNullOrWhiteSpace(gcNameAr))
            {
                return gcNameAr.Trim();
            }

            if (!string.IsNullOrWhiteSpace(gcName))
            {
                return gcName.Trim();
            }

            return null;
        }

        private static PmsHotelFinanceReportRowDto MapPaymentReceiptRow(
            PaymentReceipt pr,
            HotelReservationMeta meta)
        {
            var amount = Math.Round(Math.Abs(pr.AmountPaid), 2, MidpointRounding.AwayFromZero);
            return new PmsHotelFinanceReportRowDto
            {
                ReservationRouteId = meta.RouteId,
                ReservationZaaerId = meta.ZaaerId,
                ReservationNo = meta.ReservationNo,
                UnitLabel = meta.UnitLabel,
                CustomerName = meta.CustomerName,
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

        private static PmsHotelFinanceReportRowDto MapInvoiceRow(
            Invoice inv,
            IReadOnlyDictionary<int, HotelReservationMeta> metaByKey)
        {
            metaByKey.TryGetValue(inv.ReservationId ?? -1, out var meta);
            return new PmsHotelFinanceReportRowDto
            {
                ReservationRouteId = meta?.RouteId ?? 0,
                ReservationZaaerId = meta?.ZaaerId,
                ReservationNo = meta?.ReservationNo ?? string.Empty,
                UnitLabel = meta?.UnitLabel,
                CustomerName = meta?.CustomerName,
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

        private static PmsHotelFinanceReportRowDto MapCreditNoteRow(
            CreditNote cn,
            IReadOnlyDictionary<int, HotelReservationMeta> metaByKey,
            Invoice? linked)
        {
            metaByKey.TryGetValue(cn.ReservationId ?? -1, out var meta);

            return new PmsHotelFinanceReportRowDto
            {
                ReservationRouteId = meta?.RouteId ?? 0,
                ReservationZaaerId = meta?.ZaaerId,
                ReservationNo = meta?.ReservationNo ?? string.Empty,
                UnitLabel = meta?.UnitLabel,
                CustomerName = meta?.CustomerName,
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

        private static PmsHotelFinanceReportDto BuildFinanceReport(IReadOnlyList<PmsHotelFinanceReportRowDto> rows) =>
            new()
            {
                Items = rows,
                Summary = new PmsHallFinanceReportSummaryDto
                {
                    Count = rows.Count,
                    TotalAmount = Math.Round(rows.Sum(r => r.Amount), 2, MidpointRounding.AwayFromZero)
                }
            };

        private static PmsHotelBookingsReportDto EmptyBookingsReport() =>
            new()
            {
                Items = Array.Empty<PmsHotelBookingsReportRowDto>(),
                Summary = new PmsHotelBookingsReportSummaryDto()
            };

        private static PmsHotelDeparturesReportDto EmptyDeparturesReport() =>
            new()
            {
                Items = Array.Empty<PmsHotelDeparturesReportRowDto>(),
                Summary = new PmsHotelDeparturesReportSummaryDto()
            };

        private static PmsHotelOnlineBookingsReportDto EmptyOnlineBookingsReport() =>
            new()
            {
                Items = Array.Empty<PmsHotelOnlineBookingsReportRowDto>(),
                Summary = new PmsHotelOnlineBookingsReportSummaryDto()
            };

        private static PmsHotelUnitTransfersReportDto EmptyUnitTransfersReport() =>
            new()
            {
                Items = Array.Empty<PmsHotelUnitTransfersReportRowDto>(),
                Summary = new PmsHotelUnitTransfersReportSummaryDto()
            };

        private static bool IsCancelledReservation(Reservation reservation)
        {
            var status = (reservation.Status ?? string.Empty).Trim().ToLowerInvariant()
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace("_", string.Empty, StringComparison.Ordinal);
            return status is "cancelled" or "canceled";
        }

        private static bool IsReceptionSource(string? source) =>
            string.Equals((source ?? string.Empty).Trim(), "Reception", StringComparison.OrdinalIgnoreCase);

        private static string? NormalizeOnlineBookingSourceLabel(string? source)
        {
            var trimmed = (source ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        private static DateTime ResolveReservationDepartureDate(
            Reservation reservation,
            IReadOnlyList<ReservationUnit> units)
        {
            if (units.Count > 0)
            {
                return units
                    .Select(u => ResolveUnitDepartureDate(u, reservation))
                    .Max();
            }

            if (reservation.DepartureDate.HasValue)
            {
                return reservation.DepartureDate.Value.Date;
            }

            return reservation.CheckOutDate?.Date ?? reservation.ReservationDate.Date;
        }

        private static DateTime ResolveUnitDepartureDate(ReservationUnit unit, Reservation reservation)
        {
            if (unit.DepartureDate.HasValue)
            {
                return unit.DepartureDate.Value.Date;
            }

            if (reservation.DepartureDate.HasValue)
            {
                return reservation.DepartureDate.Value.Date;
            }

            return unit.CheckOutDate.Date;
        }

        private static PmsHotelFinanceReportDto EmptyFinanceReport() =>
            new()
            {
                Items = Array.Empty<PmsHotelFinanceReportRowDto>(),
                Summary = new PmsHallFinanceReportSummaryDto()
            };

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

            return BuildCustomerLookup(customers);
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

        private sealed record HotelScope(int LocalHotelId, int ScopeHotelId);

        private sealed class HotelReservationMeta
        {
            public int RouteId { get; set; }
            public int? ZaaerId { get; set; }
            public string ReservationNo { get; set; } = string.Empty;
            public string? CustomerName { get; set; }
            public string? UnitLabel { get; set; }
        }
    }
}
