using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Repositories.Interfaces;
using zaaerIntegration.Security;
using zaaerIntegration.Services.ActivityLog;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Services.Integrations.Zatca;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    public sealed class PmsCreditNoteService : IPmsCreditNoteService
    {
        private const decimal AmountTolerance = 0.01m;

        private readonly ApplicationDbContext _context;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INumberingService _numberingService;
        private readonly ICurrentUserContext _currentUser;
        private readonly IReservationActivityLogWriter _activityLog;

        public PmsCreditNoteService(
            ApplicationDbContext context,
            IUnitOfWork unitOfWork,
            INumberingService numberingService,
            ICurrentUserContext currentUser,
            IReservationActivityLogWriter activityLog)
        {
            _context = context;
            _unitOfWork = unitOfWork;
            _numberingService = numberingService;
            _currentUser = currentUser;
            _activityLog = activityLog;
        }

        public async Task<IReadOnlyList<PmsAdjustmentRowDto>> ListByInvoiceAsync(
            int invoiceId,
            CancellationToken cancellationToken = default)
        {
            var invoice = await LoadInvoiceForUpdateAsync(invoiceId, cancellationToken);
            if (invoice == null)
            {
                return Array.Empty<PmsAdjustmentRowDto>();
            }

            var fk = PmsInvoiceService.ResolveInvoiceForeignKey(invoice);
            return await _context.CreditNotes
                .AsNoTracking()
                .Where(c => c.HotelId == invoice.HotelId && c.InvoiceId == fk)
                .OrderByDescending(c => c.CreditNoteDate)
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
        }

        public async Task<int> CountByReservationAsync(
            int reservationId,
            CancellationToken cancellationToken = default)
        {
            var reservation = await LoadReservationAsync(reservationId, cancellationToken);
            if (reservation == null)
            {
                return 0;
            }

            return await BuildReservationCreditNoteQuery(reservation.Value).CountAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PmsCreditNoteReservationRowDto>> ListByReservationAsync(
            int reservationId,
            CancellationToken cancellationToken = default)
        {
            var reservation = await LoadReservationAsync(reservationId, cancellationToken);
            if (reservation == null)
            {
                return Array.Empty<PmsCreditNoteReservationRowDto>();
            }

            var res = reservation.Value;

            var rows = await (
                    from cn in BuildReservationCreditNoteQuery(res)
                    join inv in _context.Invoices.AsNoTracking()
                        on cn.InvoiceId equals inv.ZaaerId into invJoin
                    from inv in invJoin.DefaultIfEmpty()
                    orderby cn.CreditNoteDate descending, cn.CreditNoteId descending
                    select new PmsCreditNoteReservationRowDto
                    {
                        CreditNoteId = cn.CreditNoteId,
                        ZaaerId = cn.ZaaerId,
                        CreditNoteNo = cn.CreditNoteNo,
                        CreditNoteDate = cn.CreditNoteDate,
                        CreditAmount = cn.CreditAmount,
                        InvoiceId = cn.InvoiceId,
                        InvoiceNo = inv != null ? inv.InvoiceNo : null,
                        ZatcaStatus = cn.ZatcaStatus ?? ZatcaApiConstants.StatusPending,
                        Reason = cn.Reason
                    })
                .ToListAsync(cancellationToken);

            return rows;
        }

        public async Task<PmsCreditNoteReservationRowDto?> GetByZaaerIdAsync(
            int zaaerId,
            CancellationToken cancellationToken = default)
        {
            if (zaaerId <= 0)
            {
                return null;
            }

            return await (
                    from cn in _context.CreditNotes.AsNoTracking()
                    where cn.ZaaerId == zaaerId
                    join inv in _context.Invoices.AsNoTracking()
                        on cn.InvoiceId equals inv.ZaaerId into invJoin
                    from inv in invJoin.DefaultIfEmpty()
                    select new PmsCreditNoteReservationRowDto
                    {
                        CreditNoteId = cn.CreditNoteId,
                        ZaaerId = cn.ZaaerId,
                        CreditNoteNo = cn.CreditNoteNo,
                        CreditNoteDate = cn.CreditNoteDate,
                        CreditAmount = cn.CreditAmount,
                        InvoiceId = cn.InvoiceId,
                        InvoiceNo = inv != null ? inv.InvoiceNo : null,
                        InvoiceZaaerId = inv != null ? inv.ZaaerId : null,
                        ZatcaStatus = cn.ZatcaStatus ?? ZatcaApiConstants.StatusPending,
                        CreditType = cn.CreditType,
                        Reason = cn.Reason,
                        Notes = cn.Notes,
                        ReservationId = cn.ReservationId,
                        HotelId = cn.HotelId
                    })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<PmsAdjustmentRowDto> CreateAsync(
            PmsCreateCreditNoteDto dto,
            CancellationToken cancellationToken = default)
        {
            if (dto.CreditAmount <= 0m)
            {
                throw new ArgumentException("Credit amount must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(dto.Reason))
            {
                throw new ArgumentException("Reason is required.");
            }

            var invoice = await LoadInvoiceForUpdateAsync(dto.InvoiceId, cancellationToken);
            if (invoice == null)
            {
                throw new ArgumentException("Invoice not found.");
            }

            if (dto.HotelId != invoice.HotelId)
            {
                throw new ArgumentException("HotelId does not match the invoice.");
            }

            var remaining = await GetInvoiceRemainingAmountAsync(invoice, cancellationToken);
            if (dto.CreditAmount > remaining + AmountTolerance)
            {
                throw new ArgumentException(
                    $"Credit amount ({dto.CreditAmount:N2}) exceeds remaining invoice balance ({remaining:N2}).");
            }

            var profile = await ZatcaReservationLinkage.ResolveCreditNoteTypeAsync(
                _context,
                invoice.ReservationId,
                invoice.HotelId,
                cancellationToken);
            var auditIds = new List<long>();
            var pmsUserId = PmsCurrentUser.ResolveUserId(_currentUser, dto.CreatedBy);

            try
            {
                var identity = await _numberingService.GetNextBusinessIdentityAsync(
                    "credit_note",
                    dto.HotelId,
                    pmsUserId?.ToString() ?? "pms",
                    $"pms-credit:{dto.HotelId}:{dto.InvoiceId}:{Guid.NewGuid():N}",
                    cancellationToken);
                auditIds.Add(identity.AuditId);

                var creditNote = new CreditNote
                {
                    CreditNoteNo = identity.DocumentNo,
                    ZaaerId = ZaaerIdMapper.ToNullableInt32(identity.ZaaerId),
                    HotelId = dto.HotelId,
                    InvoiceId = PmsInvoiceService.ResolveInvoiceForeignKey(invoice),
                    ReservationId = invoice.ReservationId,
                    CustomerId = invoice.CustomerId,
                    OrderId = invoice.OrderId,
                    CreditNoteDate = KsaTime.Now.Date,
                    CreditAmount = Math.Round(dto.CreditAmount, 2, MidpointRounding.AwayFromZero),
                    Reason = dto.Reason.Trim(),
                    CreditType = profile,
                    Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                    IsSentZatca = false,
                    ZatcaStatus = ZatcaApiConstants.StatusPending,
                    ZatcaUuid = Guid.NewGuid().ToString(),
                    CreatedBy = pmsUserId,
                    CreatedAt = KsaTime.Now
                };

                await DocumentTaxComputation.ApplyCreditNoteTaxesAsync(_context, creditNote, cancellationToken);

                await _context.CreditNotes.AddAsync(creditNote, cancellationToken);
                await PmsInvoiceService.MarkInvoiceReversedAsync(_context, _unitOfWork, invoice, cancellationToken);

                foreach (var auditId in auditIds)
                {
                    await _numberingService.MarkCommittedAsync(auditId, cancellationToken);
                }

                var reservationCtx = await ResolveReservationActivityContextAsync(
                    invoice.ReservationId,
                    cancellationToken);
                if (reservationCtx != null)
                {
                    await _activityLog.LogAsync(
                        new ReservationActivityLogEntry
                        {
                            EventKey = ReservationActivityEvents.CreditNoteCreated,
                            HotelId = dto.HotelId,
                            ReservationId = reservationCtx.Value.ReservationId,
                            ReservationNo = reservationCtx.Value.ReservationNo,
                            RefType = "CreditNote",
                            RefId = creditNote.CreditNoteId,
                            RefNo = creditNote.CreditNoteNo,
                            AmountTo = creditNote.CreditAmount,
                            IconKey = "undo",
                            Payload = new Dictionary<string, object?>
                            {
                                ["creditNoteNo"] = creditNote.CreditNoteNo,
                                ["invoiceNo"] = invoice.InvoiceNo,
                                ["amount"] = creditNote.CreditAmount,
                                ["reservationNo"] = reservationCtx.Value.ReservationNo
                            },
                            ZaaerId = creditNote.ZaaerId
                        },
                        cancellationToken);
                }

                return new PmsAdjustmentRowDto
                {
                    Kind = "credit_note",
                    DocumentId = creditNote.CreditNoteId,
                    ZaaerId = creditNote.ZaaerId,
                    DocumentNo = creditNote.CreditNoteNo,
                    DocumentDate = creditNote.CreditNoteDate,
                    Amount = creditNote.CreditAmount,
                    ZatcaStatus = creditNote.ZatcaStatus,
                    Reason = creditNote.Reason
                };
            }
            catch (Exception ex)
            {
                foreach (var auditId in auditIds)
                {
                    await _numberingService.MarkVoidedAsync(auditId, ex.Message, cancellationToken);
                }

                throw;
            }
        }

        private async Task<Invoice?> LoadInvoiceForUpdateAsync(int invoiceId, CancellationToken cancellationToken) =>
            await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken);

        private async Task<(int ReservationId, int? ZaaerId, int HotelId)?> LoadReservationAsync(
            int routeId,
            CancellationToken cancellationToken)
        {
            var reservation = await PmsReservationRouteResolver.FindAsync(
                _context,
                routeId,
                cancellationToken: cancellationToken);

            if (reservation == null)
            {
                return null;
            }

            return (reservation.ReservationId, reservation.ZaaerId, reservation.HotelId);
        }

        private IQueryable<CreditNote> BuildReservationCreditNoteQuery(
            (int ReservationId, int? ZaaerId, int HotelId) reservation)
        {
            var reservationRefs = new List<int> { reservation.ReservationId };
            if (reservation.ZaaerId is > 0)
            {
                reservationRefs.Add(reservation.ZaaerId.Value);
            }

            return _context.CreditNotes.AsNoTracking()
                .Where(cn => cn.HotelId == reservation.HotelId)
                .Where(cn =>
                    (cn.ReservationId != null && reservationRefs.Contains(cn.ReservationId.Value))
                    || _context.Invoices.Any(i =>
                        i.HotelId == reservation.HotelId
                        && cn.InvoiceId == (i.ZaaerId ?? i.InvoiceId)
                        && i.ReservationId != null
                        && reservationRefs.Contains(i.ReservationId.Value)));
        }

        private async Task<decimal> GetInvoiceRemainingAmountAsync(Invoice invoice, CancellationToken cancellationToken)
        {
            var total = invoice.TotalAmount ?? 0m;
            var fk = PmsInvoiceService.ResolveInvoiceForeignKey(invoice);

            var credits = await _context.CreditNotes
                .AsNoTracking()
                .Where(c => c.HotelId == invoice.HotelId && c.InvoiceId == fk)
                .SumAsync(c => (decimal?)c.CreditAmount, cancellationToken) ?? 0m;

            var debits = await _context.DebitNotes
                .AsNoTracking()
                .Where(d => d.HotelId == invoice.HotelId && d.InvoiceId == fk)
                .SumAsync(d => (decimal?)d.DebitAmount, cancellationToken) ?? 0m;

            return Math.Max(0m, total - credits + debits);
        }

        private async Task<(int ReservationId, string ReservationNo)?> ResolveReservationActivityContextAsync(
            int? reservationZaaerOrInternalId,
            CancellationToken cancellationToken)
        {
            if (reservationZaaerOrInternalId is not > 0)
            {
                return null;
            }

            var id = reservationZaaerOrInternalId.Value;
            var reservation = await _context.Reservations
                .AsNoTracking()
                .Where(r => r.ReservationId == id || r.ZaaerId == id)
                .Select(r => new { r.ReservationId, r.ReservationNo })
                .FirstOrDefaultAsync(cancellationToken);

            if (reservation == null)
            {
                return null;
            }

            return (reservation.ReservationId, reservation.ReservationNo ?? string.Empty);
        }

    }
}
