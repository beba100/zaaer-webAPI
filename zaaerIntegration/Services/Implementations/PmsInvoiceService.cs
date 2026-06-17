using FinanceLedgerAPI.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.DTOs.Pms.ReservationDetail;
using zaaerIntegration.Repositories.Interfaces;
using zaaerIntegration.Security;
using zaaerIntegration.Services.ActivityLog;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Services.Integrations.Zatca;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    public sealed class PmsInvoiceService : IPmsInvoiceService
    {
        private const decimal AmountTolerance = 0.01m;
        public const string InvoiceTypeSales = "sales_invoice";
        public const string PaymentStatusPaid = "paid";
        public const string PaymentStatusReversed = "reversed";

        private readonly ApplicationDbContext _context;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INumberingService _numberingService;
        private readonly IReservationDetailService _reservationDetail;
        private readonly ICurrentUserContext _currentUser;
        private readonly IReservationActivityLogWriter _activityLog;

        public PmsInvoiceService(
            ApplicationDbContext context,
            IUnitOfWork unitOfWork,
            INumberingService numberingService,
            IReservationDetailService reservationDetail,
            ICurrentUserContext currentUser,
            IReservationActivityLogWriter activityLog)
        {
            _context = context;
            _unitOfWork = unitOfWork;
            _numberingService = numberingService;
            _reservationDetail = reservationDetail;
            _currentUser = currentUser;
            _activityLog = activityLog;
        }

        public async Task<IReadOnlyList<PmsInvoiceRowDto>> ListByReservationAsync(
            int reservationId,
            CancellationToken cancellationToken = default)
        {
            var reservation = await LoadReservationAsync(reservationId, cancellationToken);
            if (reservation == null)
            {
                return Array.Empty<PmsInvoiceRowDto>();
            }

            var query = BuildReservationInvoiceQuery(reservation.ReservationId, reservation.ZaaerId);

            var rows = await query
                .OrderByDescending(i => i.InvoiceDate)
                .ThenByDescending(i => i.InvoiceId)
                .Select(i => new
                {
                    i.InvoiceId,
                    i.ZaaerId,
                    i.InvoiceNo,
                    i.InvoiceDate,
                    i.TotalAmount,
                    i.PeriodFrom,
                    i.PeriodTo,
                    i.ZatcaStatus,
                    i.CustomerId,
                    i.HotelId,
                    i.ReservationId,
                    i.Notes,
                    i.PaymentStatus,
                    i.ZatcaUuid
                })
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                return Array.Empty<PmsInvoiceRowDto>();
            }

            var customerIds = rows
                .Where(r => r.CustomerId.HasValue)
                .Select(r => r.CustomerId!.Value)
                .Distinct()
                .ToList();

            var customerNames = new Dictionary<int, string>();
            if (customerIds.Count > 0)
            {
                var customers = await _context.Customers
                    .AsNoTracking()
                    .Where(c => customerIds.Contains(c.CustomerId) || (c.ZaaerId != null && customerIds.Contains(c.ZaaerId.Value)))
                    .Select(c => new { c.CustomerId, c.ZaaerId, c.CustomerName })
                    .ToListAsync(cancellationToken);

                foreach (var c in customers)
                {
                    customerNames[c.CustomerId] = c.CustomerName;
                    if (c.ZaaerId is > 0)
                    {
                        customerNames[c.ZaaerId.Value] = c.CustomerName;
                    }
                }
            }

            var hotelId = rows[0].HotelId;
            var invoiceFks = rows
                .Select(r => r.ZaaerId ?? r.InvoiceId)
                .Distinct()
                .ToList();

            var creditAgg = await _context.CreditNotes
                .AsNoTracking()
                .Where(c => c.HotelId == hotelId && invoiceFks.Contains(c.InvoiceId))
                .GroupBy(c => c.InvoiceId)
                .Select(g => new { InvoiceFk = g.Key, Count = g.Count(), Sum = g.Sum(c => c.CreditAmount) })
                .ToListAsync(cancellationToken);

            var debitAgg = await _context.DebitNotes
                .AsNoTracking()
                .Where(d => d.HotelId == hotelId && invoiceFks.Contains(d.InvoiceId))
                .GroupBy(d => d.InvoiceId)
                .Select(g => new { InvoiceFk = g.Key, Count = g.Count(), Sum = g.Sum(d => d.DebitAmount) })
                .ToListAsync(cancellationToken);

            var relatedByFk = invoiceFks.ToDictionary(fk => fk, _ => 0);
            var creditByFk = invoiceFks.ToDictionary(fk => fk, _ => 0);
            var creditSumByFk = invoiceFks.ToDictionary(fk => fk, _ => 0m);
            var debitSumByFk = invoiceFks.ToDictionary(fk => fk, _ => 0m);
            foreach (var c in creditAgg)
            {
                relatedByFk[c.InvoiceFk] = relatedByFk.GetValueOrDefault(c.InvoiceFk) + c.Count;
                creditByFk[c.InvoiceFk] = c.Count;
                creditSumByFk[c.InvoiceFk] = c.Sum;
            }

            foreach (var d in debitAgg)
            {
                relatedByFk[d.InvoiceFk] = relatedByFk.GetValueOrDefault(d.InvoiceFk) + d.Count;
                debitSumByFk[d.InvoiceFk] = d.Sum;
            }

            return rows.Select(r =>
            {
                var invoiceFk = r.ZaaerId ?? r.InvoiceId;
                var total = r.TotalAmount ?? 0m;
                var credits = creditSumByFk.GetValueOrDefault(invoiceFk);
                var debits = debitSumByFk.GetValueOrDefault(invoiceFk);
                return new PmsInvoiceRowDto
                {
                    InvoiceId = r.InvoiceId,
                    ZaaerId = r.ZaaerId,
                    InvoiceNo = r.InvoiceNo,
                    InvoiceDate = r.InvoiceDate,
                    TotalAmount = r.TotalAmount,
                    PeriodFrom = r.PeriodFrom,
                    PeriodTo = r.PeriodTo,
                    ZatcaStatus = r.ZatcaStatus ?? ZatcaApiConstants.StatusPending,
                    CustomerId = r.CustomerId,
                    CustomerName = r.CustomerId.HasValue && customerNames.TryGetValue(r.CustomerId.Value, out var name)
                        ? name
                        : null,
                    HotelId = r.HotelId,
                    ReservationId = r.ReservationId,
                    Notes = r.Notes,
                    PaymentStatus = r.PaymentStatus ?? "unpaid",
                    ParentZatcaSubmitted = IsZatcaSubmitted(r.ZatcaStatus, r.ZatcaUuid),
                    RelatedAdjustmentCount = relatedByFk.GetValueOrDefault(invoiceFk),
                    RelatedCreditNoteCount = creditByFk.GetValueOrDefault(invoiceFk),
                    AdjustmentRemainingAmount = Math.Max(0m, total - credits + debits)
                };
            }).ToList();
        }

        public async Task<PmsInvoiceContextDto> GetCreateContextAsync(
            int reservationId,
            CancellationToken cancellationToken = default)
        {
            var snapshot = await _reservationDetail.GetCheckoutSnapshotAsync(reservationId, null, cancellationToken);
            if (snapshot == null)
            {
                throw new ArgumentException("Reservation not found.");
            }

            var detail = await _reservationDetail.GetByZaaerOrReservationIdAsync(reservationId, null, cancellationToken);
            if (detail == null)
            {
                throw new ArgumentException("Reservation not found.");
            }

            var periodFrom = detail.Dates.CheckInDate?.Date;
            var periodTo = (detail.Dates.DepartureDate ?? detail.Dates.CheckOutDate)?.Date;

            var lastInvoice = await BuildReservationInvoiceQuery(detail.ReservationId, detail.ZaaerId)
                .Where(i =>
                    i.PaymentStatus == null
                    || (i.PaymentStatus != "void"
                        && i.PaymentStatus != "voided"
                        && i.PaymentStatus != "cancelled"
                        && i.PaymentStatus != "canceled"))
                .OrderByDescending(i => i.InvoiceDate)
                .ThenByDescending(i => i.InvoiceId)
                .Select(i => new PmsInvoiceLastInvoiceDto
                {
                    InvoiceNo = i.InvoiceNo,
                    TotalAmount = i.TotalAmount,
                    PeriodFrom = i.PeriodFrom,
                    PeriodTo = i.PeriodTo
                })
                .FirstOrDefaultAsync(cancellationToken);

            var invoiceRemaining = Math.Max(0m, snapshot.InvoiceRemaining);

            return new PmsInvoiceContextDto
            {
                ReservationId = detail.ReservationId,
                HotelId = detail.HotelId,
                PaymentBalanceAmount = Math.Max(0m, snapshot.BalanceAmount),
                InvoiceRemainingAmount = invoiceRemaining,
                GrossInvoicedAmount = snapshot.GrossInvoicedTotal,
                NetInvoicedAmount = snapshot.NetInvoicedTotal,
                CreditNotesTotal = snapshot.CreditNotesTotal,
                InvoiceRequiredAmount = snapshot.InvoiceRequiredAmount,
                BalanceAmount = invoiceRemaining,
                DefaultPeriodFrom = periodFrom,
                DefaultPeriodTo = periodTo,
                VatRate = detail.PricingTax?.VatRate,
                LodgingTaxRate = detail.PricingTax?.EwaRate,
                LastInvoice = lastInvoice
            };
        }

        public async Task<IReadOnlyList<PmsAdjustmentRowDto>> ListAdjustmentsByInvoiceAsync(
            int invoiceId,
            CancellationToken cancellationToken = default)
        {
            var invoice = await _context.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken);
            if (invoice == null)
            {
                return Array.Empty<PmsAdjustmentRowDto>();
            }

            var fk = ResolveInvoiceForeignKey(invoice);

            var credits = await _context.CreditNotes
                .AsNoTracking()
                .Where(c => c.HotelId == invoice.HotelId && c.InvoiceId == fk)
                .Select(c => new PmsAdjustmentRowDto
                {
                    Kind = "credit_note",
                    DocumentId = c.CreditNoteId,
                    ZaaerId = c.ZaaerId,
                    DocumentNo = c.CreditNoteNo,
                    DocumentDate = c.CreditNoteDate,
                    Amount = c.CreditAmount,
                    ZatcaStatus = c.ZatcaStatus ?? ZatcaApiConstants.StatusPending,
                    Reason = c.Reason
                })
                .ToListAsync(cancellationToken);

            var debits = await _context.DebitNotes
                .AsNoTracking()
                .Where(d => d.HotelId == invoice.HotelId && d.InvoiceId == fk)
                .Select(d => new PmsAdjustmentRowDto
                {
                    Kind = "debit_note",
                    DocumentId = d.DebitNoteId,
                    ZaaerId = d.ZaaerId,
                    DocumentNo = d.DebitNoteNo,
                    DocumentDate = d.DebitNoteDate,
                    Amount = d.DebitAmount,
                    ZatcaStatus = d.ZatcaStatus ?? ZatcaApiConstants.StatusPending,
                    Reason = d.Reason
                })
                .ToListAsync(cancellationToken);

            return credits.Concat(debits).OrderByDescending(x => x.DocumentDate).ToList();
        }

        public async Task<PmsInvoiceRowDto> CreateAsync(
            PmsCreateInvoiceDto dto,
            CancellationToken cancellationToken = default)
        {
            if (dto.TotalAmount <= 0m)
            {
                throw new ArgumentException("Invoice amount must be greater than zero.");
            }

            var detail = await _reservationDetail.GetByZaaerOrReservationIdAsync(dto.ReservationId, null, cancellationToken);
            if (detail == null)
            {
                throw new ArgumentException("Reservation not found.");
            }

            if (dto.HotelId != detail.HotelId)
            {
                throw new ArgumentException("HotelId does not match the reservation.");
            }

            var snapshot = await _reservationDetail.GetCheckoutSnapshotAsync(
                dto.ReservationId,
                dto.HotelId,
                cancellationToken);
            if (snapshot == null)
            {
                throw new ArgumentException("Reservation not found.");
            }

            var invoiceRemaining = Math.Max(0m, snapshot.InvoiceRemaining);
            if (dto.TotalAmount > invoiceRemaining + AmountTolerance)
            {
                throw new ArgumentException(
                    $"Invoice amount ({dto.TotalAmount:N2}) exceeds remaining to invoice ({invoiceRemaining:N2}).");
            }

            if (invoiceRemaining <= AmountTolerance)
            {
                throw new ArgumentException("reservationDetail.payments.invoice.noBalance");
            }

            var periodFrom = dto.PeriodFrom?.Date ?? detail.Dates.CheckInDate?.Date;
            var periodTo = dto.PeriodTo?.Date ?? (detail.Dates.DepartureDate ?? detail.Dates.CheckOutDate)?.Date;

            var reservationZaaerId = ResolveReservationZaaerId(detail);
            var customerZaaerId = ResolveCustomerZaaerId(detail);
            var roundedTotal = Math.Round(dto.TotalAmount, 2, MidpointRounding.AwayFromZero);

            const int maxAttempts = 3;
            var pmsUserId = PmsCurrentUser.ResolveUserId(_currentUser, dto.CreatedBy);
            var auditIds = new List<long>();
            Exception? lastError = null;

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var identity = await _numberingService.GetNextBusinessIdentityAsync(
                    "invoice",
                    dto.HotelId,
                    pmsUserId?.ToString() ?? "pms",
                    $"pms-invoice:{dto.HotelId}:{dto.ReservationId}:{Guid.NewGuid():N}",
                    cancellationToken);
                auditIds.Add(identity.AuditId);

                var invoice = new Invoice
                {
                    InvoiceNo = identity.DocumentNo,
                    ZaaerId = ZaaerIdMapper.ToNullableInt32(identity.ZaaerId),
                    HotelId = dto.HotelId,
                    ReservationId = reservationZaaerId,
                    CustomerId = customerZaaerId,
                    InvoiceDate = (dto.InvoiceDate ?? KsaTime.Now).Date,
                    InvoiceType = InvoiceTypeSales,
                    TotalAmount = roundedTotal,
                    PeriodFrom = periodFrom,
                    PeriodTo = periodTo,
                    Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                    PaymentStatus = PaymentStatusPaid,
                    AmountPaid = roundedTotal,
                    AmountRemaining = 0m,
                    IsSentZatca = false,
                    ZatcaStatus = ZatcaApiConstants.StatusPending,
                    ZatcaUuid = Guid.NewGuid().ToString(),
                    CreatedBy = pmsUserId,
                    CreatedAt = KsaTime.Now
                };

                try
                {
                    await DocumentTaxComputation.ApplyInvoiceTaxesAsync(_context, invoice, cancellationToken);

                    await _context.Invoices.AddAsync(invoice, cancellationToken);
                    await _unitOfWork.SaveChangesAsync();

                    await _numberingService.MarkCommittedAsync(identity.AuditId, cancellationToken);

                    await _activityLog.LogAsync(
                        new ReservationActivityLogEntry
                        {
                            EventKey = ReservationActivityEvents.InvoiceCreated,
                            HotelId = dto.HotelId,
                            ReservationId = detail.ReservationId,
                            ReservationNo = detail.Header.ReservationNo,
                            RefType = "Invoice",
                            RefId = invoice.InvoiceId,
                            RefNo = invoice.InvoiceNo,
                            AmountTo = invoice.TotalAmount,
                            IconKey = "money",
                            Payload = new Dictionary<string, object?>
                            {
                                ["invoiceNo"] = invoice.InvoiceNo,
                                ["amount"] = invoice.TotalAmount,
                                ["reservationNo"] = detail.Header.ReservationNo
                            },
                            ZaaerId = invoice.ZaaerId
                        },
                        cancellationToken);

                    var customerName = await ResolveCustomerNameAsync(invoice.CustomerId, cancellationToken);
                    return MapRow(invoice, customerName);
                }
                catch (DbUpdateException ex) when (IsDuplicateInvoiceNo(ex))
                {
                    lastError = ex;
                    _context.Entry(invoice).State = EntityState.Detached;
                    await _numberingService.MarkVoidedAsync(identity.AuditId, ex.Message, cancellationToken);

                    var existing = await _context.Invoices
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            i => i.HotelId == dto.HotelId && i.InvoiceNo == identity.DocumentNo,
                            cancellationToken);

                    if (existing != null && InvoiceMatchesReservation(existing, detail))
                    {
                        var existingCustomerName = await ResolveCustomerNameAsync(existing.CustomerId, cancellationToken);
                        return MapRow(existing, existingCustomerName);
                    }

                    if (attempt == maxAttempts - 1)
                    {
                        throw new InvalidOperationException(
                            $"Invoice number '{identity.DocumentNo}' is already in use. Please try again.",
                            ex);
                    }
                }
                catch (Exception ex)
                {
                    _context.Entry(invoice).State = EntityState.Detached;
                    await _numberingService.MarkVoidedAsync(identity.AuditId, ex.Message, cancellationToken);
                    throw;
                }
            }

            throw lastError ?? new InvalidOperationException("Could not create invoice.");
        }

        private IQueryable<Invoice> BuildReservationInvoiceQuery(int reservationId, int? reservationZaaerId)
        {
            var query = _context.Invoices.AsNoTracking();
            if (reservationZaaerId.HasValue && reservationZaaerId.Value > 0)
            {
                var zid = reservationZaaerId.Value;
                return query.Where(i => i.ReservationId == zid || i.ReservationId == reservationId);
            }

            return query.Where(i => i.ReservationId == reservationId);
        }

        private async Task<ReservationRef?> LoadReservationAsync(int routeId, CancellationToken cancellationToken)
        {
            var reservation = await PmsReservationRouteResolver.FindAsync(
                _context,
                routeId,
                cancellationToken: cancellationToken);

            return reservation == null
                ? null
                : new ReservationRef(reservation.ReservationId, reservation.ZaaerId, reservation.HotelId);
        }

        private static PmsInvoiceRowDto MapRow(Invoice invoice, string? customerName) =>
            new()
            {
                InvoiceId = invoice.InvoiceId,
                ZaaerId = invoice.ZaaerId,
                InvoiceNo = invoice.InvoiceNo,
                InvoiceDate = invoice.InvoiceDate,
                TotalAmount = invoice.TotalAmount,
                PeriodFrom = invoice.PeriodFrom,
                PeriodTo = invoice.PeriodTo,
                ZatcaStatus = invoice.ZatcaStatus ?? ZatcaApiConstants.StatusPending,
                CustomerId = invoice.CustomerId,
                CustomerName = customerName,
                HotelId = invoice.HotelId,
                ReservationId = invoice.ReservationId,
                Notes = invoice.Notes,
                PaymentStatus = invoice.PaymentStatus,
                ParentZatcaSubmitted = IsZatcaSubmitted(invoice.ZatcaStatus, invoice.ZatcaUuid)
            };

        internal static int ResolveInvoiceForeignKey(Invoice invoice) =>
            invoice.ZaaerId ?? invoice.InvoiceId;

        internal static bool IsZatcaSubmitted(string? status, string? uuid) =>
            !string.IsNullOrWhiteSpace(uuid)
            && (string.Equals(status, ZatcaApiConstants.StatusReported, StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, ZatcaApiConstants.StatusCleared, StringComparison.OrdinalIgnoreCase));

        internal static bool IsVoidedPaymentStatus(string? paymentStatus)
        {
            var norm = paymentStatus?.Trim().ToLowerInvariant();
            return norm is "void" or "voided" or "cancelled" or "canceled";
        }

        private static bool IsDuplicateInvoiceNo(DbUpdateException ex)
        {
            for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
            {
                if (inner is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
                {
                    return sql.Message.Contains("IX_invoices_invoice_no", StringComparison.OrdinalIgnoreCase)
                           || sql.Message.Contains("invoice_no", StringComparison.OrdinalIgnoreCase);
                }
            }

            return false;
        }

        private static bool InvoiceMatchesReservation(Invoice invoice, ReservationDetailDto detail)
        {
            var resZaaer = ResolveReservationZaaerId(detail);
            return invoice.ReservationId == resZaaer
                   || invoice.ReservationId == detail.ReservationId
                   || (detail.ZaaerId.HasValue && invoice.ReservationId == detail.ZaaerId.Value);
        }

        /// <summary>Invoices store <c>reservations.zaaer_id</c> in <c>reservation_id</c>.</summary>
        internal static int ResolveReservationZaaerId(ReservationDetailDto detail)
        {
            if (detail.ZaaerId is > 0)
            {
                return detail.ZaaerId.Value;
            }

            throw new ArgumentException(
                "Reservation has no Zaaer id. Set hotel_settings.zaaer_id and sync the reservation before invoicing.");
        }

        /// <summary>Invoices store <c>customers.zaaer_id</c> in <c>customer_id</c> when available.</summary>
        internal static int? ResolveCustomerZaaerId(ReservationDetailDto detail)
        {
            if (detail.CustomerZaaerId is > 0)
            {
                return detail.CustomerZaaerId.Value;
            }

            var guestZaaer = detail.Guests.FirstOrDefault(g => g.IsPrimary)?.CustomerZaaerId;
            if (guestZaaer is > 0)
            {
                return guestZaaer.Value;
            }

            return null;
        }

        internal static async Task MarkInvoiceReversedAsync(
            ApplicationDbContext context,
            IUnitOfWork unitOfWork,
            Invoice invoice,
            CancellationToken cancellationToken = default)
        {
            invoice.PaymentStatus = PaymentStatusReversed;
            await unitOfWork.SaveChangesAsync();
        }

        private async Task<string?> ResolveCustomerNameAsync(int? customerZaaerOrInternalId, CancellationToken cancellationToken)
        {
            if (customerZaaerOrInternalId is not > 0)
            {
                return null;
            }

            var id = customerZaaerOrInternalId.Value;
            return await _context.Customers
                .AsNoTracking()
                .Where(c => c.CustomerId == id || c.ZaaerId == id)
                .Select(c => c.CustomerName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private sealed record ReservationRef(int ReservationId, int? ZaaerId, int HotelId);
    }
}
