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
    public sealed class PmsDebitNoteService : IPmsDebitNoteService
    {
        private const decimal AmountTolerance = 0.01m;

        private readonly ApplicationDbContext _context;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INumberingService _numberingService;
        private readonly ICurrentUserContext _currentUser;
        private readonly IReservationActivityLogWriter _activityLog;

        public PmsDebitNoteService(
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
            var invoice = await _context.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken);
            if (invoice == null)
            {
                return Array.Empty<PmsAdjustmentRowDto>();
            }

            var fk = PmsInvoiceService.ResolveInvoiceForeignKey(invoice);
            return await _context.DebitNotes
                .AsNoTracking()
                .Where(d => d.HotelId == invoice.HotelId && d.InvoiceId == fk)
                .OrderByDescending(d => d.DebitNoteDate)
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
        }

        public async Task<PmsAdjustmentRowDto> CreateAsync(
            PmsCreateDebitNoteDto dto,
            CancellationToken cancellationToken = default)
        {
            if (dto.DebitAmount <= 0m)
            {
                throw new ArgumentException("Debit amount must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(dto.Reason))
            {
                throw new ArgumentException("Reason is required.");
            }

            var invoice = await _context.Invoices
                .FirstOrDefaultAsync(i => i.InvoiceId == dto.InvoiceId, cancellationToken);
            if (invoice == null)
            {
                throw new ArgumentException("Invoice not found.");
            }

            if (dto.HotelId != invoice.HotelId)
            {
                throw new ArgumentException("HotelId does not match the invoice.");
            }

            var fk = PmsInvoiceService.ResolveInvoiceForeignKey(invoice);
            var total = invoice.TotalAmount ?? 0m;
            var credits = await _context.CreditNotes
                .AsNoTracking()
                .Where(c => c.HotelId == invoice.HotelId && c.InvoiceId == fk)
                .SumAsync(c => (decimal?)c.CreditAmount, cancellationToken) ?? 0m;
            var debits = await _context.DebitNotes
                .AsNoTracking()
                .Where(d => d.HotelId == invoice.HotelId && d.InvoiceId == fk)
                .SumAsync(d => (decimal?)d.DebitAmount, cancellationToken) ?? 0m;
            var netInvoiced = total - credits + debits;
            var maxDebit = Math.Max(0m, netInvoiced);

            if (dto.DebitAmount > maxDebit + AmountTolerance)
            {
                throw new ArgumentException(
                    $"Debit amount ({dto.DebitAmount:N2}) exceeds allowable amount ({maxDebit:N2}).");
            }

            var auditIds = new List<long>();
            var pmsUserId = PmsCurrentUser.ResolveUserId(_currentUser, dto.CreatedBy);

            try
            {
                var identity = await _numberingService.GetNextBusinessIdentityAsync(
                    "debit_note",
                    dto.HotelId,
                    pmsUserId?.ToString() ?? "pms",
                    $"pms-debit:{dto.HotelId}:{dto.InvoiceId}:{Guid.NewGuid():N}",
                    cancellationToken);
                auditIds.Add(identity.AuditId);

                var debitNote = new DebitNote
                {
                    DebitNoteNo = identity.DocumentNo,
                    ZaaerId = ZaaerIdMapper.ToNullableInt32(identity.ZaaerId),
                    HotelId = dto.HotelId,
                    InvoiceId = fk,
                    ReservationId = invoice.ReservationId,
                    CustomerId = invoice.CustomerId,
                    OrderId = invoice.OrderId,
                    DebitNoteDate = KsaTime.Now.Date,
                    DebitAmount = Math.Round(dto.DebitAmount, 2, MidpointRounding.AwayFromZero),
                    OriginalInvoiceAmount = invoice.TotalAmount,
                    Reason = dto.Reason.Trim(),
                    DebitType = "adjustment",
                    Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                    IsSentZatca = false,
                    ZatcaStatus = ZatcaApiConstants.StatusPending,
                    ZatcaUuid = Guid.NewGuid().ToString(),
                    CreatedBy = pmsUserId,
                    CreatedAt = KsaTime.Now
                };

                await DocumentTaxComputation.ApplyDebitNoteTaxesAsync(_context, debitNote, cancellationToken);

                await _context.DebitNotes.AddAsync(debitNote, cancellationToken);
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
                            EventKey = ReservationActivityEvents.DebitNoteCreated,
                            HotelId = dto.HotelId,
                            ReservationId = reservationCtx.Value.ReservationId,
                            ReservationNo = reservationCtx.Value.ReservationNo,
                            RefType = "DebitNote",
                            RefId = debitNote.DebitNoteId,
                            RefNo = debitNote.DebitNoteNo,
                            AmountTo = debitNote.DebitAmount,
                            IconKey = "edit",
                            Payload = new Dictionary<string, object?>
                            {
                                ["debitNoteNo"] = debitNote.DebitNoteNo,
                                ["invoiceNo"] = invoice.InvoiceNo,
                                ["amount"] = debitNote.DebitAmount,
                                ["reservationNo"] = reservationCtx.Value.ReservationNo
                            },
                            ZaaerId = debitNote.ZaaerId
                        },
                        cancellationToken);
                }

                return new PmsAdjustmentRowDto
                {
                    Kind = "debit_note",
                    DocumentId = debitNote.DebitNoteId,
                    ZaaerId = debitNote.ZaaerId,
                    DocumentNo = debitNote.DebitNoteNo,
                    DocumentDate = debitNote.DebitNoteDate,
                    Amount = debitNote.DebitAmount,
                    ZatcaStatus = debitNote.ZatcaStatus,
                    Reason = debitNote.Reason
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
