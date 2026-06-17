using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Repositories.Interfaces;
using zaaerIntegration.Security;
using zaaerIntegration.Services.ActivityLog;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    public sealed class PmsPromissoryNoteService : IPmsPromissoryNoteService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INumberingService _numberingService;
        private readonly ICurrentUserContext _currentUser;
        private readonly IReservationFinancialSyncService _financialSync;
        private readonly IReservationActivityLogWriter _activityLog;

        public PmsPromissoryNoteService(
            ApplicationDbContext context,
            IUnitOfWork unitOfWork,
            INumberingService numberingService,
            ICurrentUserContext currentUser,
            IReservationFinancialSyncService financialSync,
            IReservationActivityLogWriter activityLog)
        {
            _context = context;
            _unitOfWork = unitOfWork;
            _numberingService = numberingService;
            _currentUser = currentUser;
            _financialSync = financialSync;
            _activityLog = activityLog;
        }

        public async Task<IReadOnlyList<PmsPromissoryNoteRowDto>> ListByReservationAsync(
            int reservationId,
            CancellationToken cancellationToken = default)
        {
            var reservation = await _context.Reservations
                .AsNoTracking()
                .Where(r =>
                    r.ReservationId == reservationId
                    || (r.ZaaerId.HasValue && r.ZaaerId.Value == reservationId))
                .Select(r => new { r.ReservationId, r.ZaaerId })
                .FirstOrDefaultAsync(cancellationToken);

            if (reservation == null)
            {
                return Array.Empty<PmsPromissoryNoteRowDto>();
            }

            var keys = ReservationFinancialSyncService.BuildReservationKeys(
                reservation.ReservationId,
                reservation.ZaaerId);

            var notes = await _context.PromissoryNotes
                .AsNoTracking()
                .Where(pn => pn.ReservationId.HasValue && keys.Contains(pn.ReservationId.Value))
                .OrderByDescending(pn => pn.CreatedAt)
                .ToListAsync(cancellationToken);

            var receiptIds = notes
                .Where(n => n.CollectionReceiptId.HasValue)
                .Select(n => n.CollectionReceiptId!.Value)
                .Distinct()
                .ToList();

            var receiptNos = receiptIds.Count == 0
                ? new Dictionary<int, string>()
                : await BuildCollectionReceiptNoLookupAsync(receiptIds, cancellationToken);

            return notes.Select(n => MapRow(n, receiptNos)).ToList();
        }

        public async Task<PmsPromissoryNoteRowDto> CreateAsync(
            PmsCreatePromissoryNoteDto dto,
            CancellationToken cancellationToken = default)
        {
            var reservation = await LoadReservationAsync(dto.ReservationId, cancellationToken);
            EnsureHotelMatch(dto.HotelId, reservation.HotelId);

            if (!reservation.ZaaerId.HasValue || reservation.ZaaerId.Value <= 0)
            {
                throw new ArgumentException("Reservation ZaaerId is required for promissory notes.");
            }

            var internalCustomerId = dto.CustomerId ?? reservation.CustomerId;
            if (!internalCustomerId.HasValue || internalCustomerId.Value <= 0)
            {
                throw new ArgumentException("CustomerId is required.");
            }

            var customerId = internalCustomerId!.Value;
            var customerZaaerId = await ResolveCustomerZaaerIdAsync(customerId, cancellationToken);
            var corporateStorageId = await ResolveCorporateStorageIdAsync(
                dto.HotelId,
                dto.CorporateId ?? reservation.CorporateId,
                cancellationToken);

            var payableTo = await ResolvePayableToAsync(dto.PayableTo, customerId, cancellationToken);
            await EnsurePromissoryAmountAllowedAsync(
                reservation,
                dto.Amount,
                excludeNoteId: null,
                cancellationToken);

            var auditIds = new List<long>();
            try
            {
                var pmsUserId = PmsCurrentUser.ResolveUserId(_currentUser, dto.CreatedBy);
                var identity = await _numberingService.GetNextBusinessIdentityAsync(
                    "promissory_note",
                    dto.HotelId,
                    pmsUserId?.ToString() ?? "pms",
                    $"pms-promissory:{dto.HotelId}:{dto.ReservationId}:{Guid.NewGuid():N}",
                    cancellationToken);

                auditIds.Add(identity.AuditId);

                var reason = string.IsNullOrWhiteSpace(dto.Reason)
                    ? BuildDefaultReason(reservation.ReservationNo)
                    : dto.Reason.Trim();

                var note = new PromissoryNote
                {
                    PromissoryNo = identity.DocumentNo,
                    ZaaerId = ZaaerIdMapper.ToNullableInt32(identity.ZaaerId),
                    HotelId = dto.HotelId,
                    ReservationId = reservation.ZaaerId.Value,
                    CustomerId = customerZaaerId,
                    CorporateId = corporateStorageId,
                    PayableTo = payableTo,
                    Reason = reason,
                    PlaceOfMaturity = string.IsNullOrWhiteSpace(dto.PlaceOfMaturity)
                        ? null
                        : dto.PlaceOfMaturity.Trim(),
                    MaturityDate = dto.MaturityDate.Date,
                    Amount = dto.Amount,
                    AmountCollected = 0m,
                    Status = "open",
                    PaymentLinkSent = dto.PaymentLinkSent,
                    Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                    CreatedBy = pmsUserId,
                    CreatedAt = KsaTime.Now
                };

                await _context.PromissoryNotes.AddAsync(note);
                await _unitOfWork.SaveChangesAsync();
                await _financialSync.SyncReservationRentPaymentTotalsAsync(
                    reservation.ReservationId,
                    cancellationToken);

                foreach (var auditId in auditIds)
                {
                    await _numberingService.MarkCommittedAsync(auditId, cancellationToken);
                }

                await _activityLog.LogAsync(
                    new ReservationActivityLogEntry
                    {
                        EventKey = ReservationActivityEvents.PromissoryCreated,
                        HotelId = dto.HotelId,
                        ReservationId = reservation.ReservationId,
                        ReservationNo = reservation.ReservationNo,
                        RefType = "PromissoryNote",
                        RefId = note.PromissoryNoteId,
                        RefNo = note.PromissoryNo,
                        AmountTo = note.Amount,
                        IconKey = "card",
                        Payload = new Dictionary<string, object?>
                        {
                            ["promissoryNo"] = note.PromissoryNo,
                            ["amount"] = note.Amount,
                            ["maturityDate"] = note.MaturityDate.ToString("yyyy-MM-dd")
                        },
                        ZaaerId = note.ZaaerId
                    },
                    cancellationToken);

                return MapRow(note, null);
            }
            catch
            {
                foreach (var auditId in auditIds)
                {
                    await _numberingService.MarkVoidedAsync(auditId, "promissory create failed", cancellationToken);
                }

                throw;
            }
        }

        public async Task<PmsPromissoryNoteRowDto> UpdateByZaaerIdAsync(
            int zaaerId,
            PmsUpdatePromissoryNoteDto dto,
            CancellationToken cancellationToken = default)
        {
            var note = await _context.PromissoryNotes
                .FirstOrDefaultAsync(pn => pn.ZaaerId == zaaerId, cancellationToken);

            if (note == null)
            {
                throw new ArgumentException($"Promissory note with ZaaerId {zaaerId} not found.");
            }

            await EnsureNoteBelongsToReservationAsync(note, dto.ReservationId, cancellationToken);
            EnsureHotelMatch(dto.HotelId, note.HotelId);
            EnsureEditable(note);

            if (dto.Amount.HasValue)
            {
                var newOutstanding = dto.Amount.Value - note.AmountCollected;
                if (newOutstanding <= 0)
                {
                    throw new ArgumentException("Promissory amount must exceed collected amount.");
                }

                await EnsurePromissoryAmountAllowedAsync(
                    await LoadReservationAsync(dto.ReservationId, cancellationToken),
                    newOutstanding,
                    note.PromissoryNoteId,
                    cancellationToken);
                note.Amount = dto.Amount.Value;
            }

            if (dto.MaturityDate.HasValue)
            {
                note.MaturityDate = dto.MaturityDate.Value.Date;
            }

            if (dto.Reason != null)
            {
                note.Reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();
            }

            if (dto.PlaceOfMaturity != null)
            {
                note.PlaceOfMaturity = string.IsNullOrWhiteSpace(dto.PlaceOfMaturity)
                    ? null
                    : dto.PlaceOfMaturity.Trim();
            }

            if (dto.Notes != null)
            {
                note.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
            }

            if (dto.PaymentLinkSent.HasValue)
            {
                note.PaymentLinkSent = dto.PaymentLinkSent.Value;
            }

            note.UpdatedAt = KsaTime.Now;
            await _unitOfWork.SaveChangesAsync();

            var reservation = await LoadReservationAsync(dto.ReservationId, cancellationToken);
            await _financialSync.SyncReservationRentPaymentTotalsAsync(
                reservation.ReservationId,
                cancellationToken);

            await _activityLog.LogAsync(
                new ReservationActivityLogEntry
                {
                    EventKey = ReservationActivityEvents.PromissoryUpdated,
                    HotelId = dto.HotelId,
                    ReservationId = reservation.ReservationId,
                    ReservationNo = reservation.ReservationNo,
                    RefType = "PromissoryNote",
                    RefId = note.PromissoryNoteId,
                    RefNo = note.PromissoryNo,
                    AmountTo = note.Amount,
                    IconKey = "edit",
                    Payload = new Dictionary<string, object?>
                    {
                        ["promissoryNo"] = note.PromissoryNo,
                        ["amount"] = note.Amount
                    },
                    ZaaerId = note.ZaaerId
                },
                cancellationToken);

            return MapRow(note, null);
        }

        public async Task<PmsPromissoryNoteRowDto> CancelByZaaerIdAsync(
            int zaaerId,
            PmsCancelPromissoryNoteDto dto,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(dto.Reason))
            {
                throw new ArgumentException("Cancellation reason is required.", nameof(dto));
            }

            var note = await _context.PromissoryNotes
                .FirstOrDefaultAsync(pn => pn.ZaaerId == zaaerId, cancellationToken);

            if (note == null)
            {
                throw new ArgumentException($"Promissory note with ZaaerId {zaaerId} not found.");
            }

            await EnsureNoteBelongsToReservationAsync(note, dto.ReservationId, cancellationToken);
            EnsureHotelMatch(dto.HotelId, note.HotelId);
            EnsureEditable(note);

            note.Status = "cancelled";
            note.Notes = string.IsNullOrWhiteSpace(note.Notes)
                ? dto.Reason.Trim()
                : $"{note.Notes.Trim()} | {dto.Reason.Trim()}";
            note.UpdatedAt = KsaTime.Now;

            await _unitOfWork.SaveChangesAsync();
            var reservation = await LoadReservationAsync(dto.ReservationId, cancellationToken);
            await _financialSync.SyncReservationRentPaymentTotalsAsync(
                reservation.ReservationId,
                cancellationToken);

            await _activityLog.LogAsync(
                new ReservationActivityLogEntry
                {
                    EventKey = ReservationActivityEvents.PromissoryCancelled,
                    HotelId = dto.HotelId,
                    ReservationId = reservation.ReservationId,
                    ReservationNo = reservation.ReservationNo,
                    RefType = "PromissoryNote",
                    RefId = note.PromissoryNoteId,
                    RefNo = note.PromissoryNo,
                    IconKey = "warning",
                    Payload = new Dictionary<string, object?>
                    {
                        ["promissoryNo"] = note.PromissoryNo,
                        ["reason"] = dto.Reason.Trim()
                    },
                    ZaaerId = note.ZaaerId
                },
                cancellationToken);

            return MapRow(note, null);
        }

        internal static async Task ApplyCollectionAsync(
            ApplicationDbContext context,
            IUnitOfWork unitOfWork,
            PromissoryNote note,
            PaymentReceipt receipt,
            decimal collectedAmount,
            CancellationToken cancellationToken)
        {
            note.AmountCollected += collectedAmount;
            note.CollectionReceiptId = receipt.ZaaerId ?? receipt.ReceiptId;
            note.UpdatedAt = KsaTime.Now;

            if (note.AmountCollected >= note.Amount)
            {
                note.AmountCollected = note.Amount;
                note.Status = "collected";
            }
            else if (note.AmountCollected > 0)
            {
                note.Status = "partial";
            }

            await unitOfWork.SaveChangesAsync();
        }

        private async Task EnsurePromissoryAmountAllowedAsync(
            Reservation reservation,
            decimal newOutstandingAmount,
            int? excludeNoteId,
            CancellationToken cancellationToken)
        {
            if (newOutstandingAmount <= 0)
            {
                throw new ArgumentException("Promissory amount must be greater than zero.");
            }

            var keys = ReservationFinancialSyncService.BuildReservationKeys(
                reservation.ReservationId,
                reservation.ZaaerId);

            var promissoryNotes = await _context.PromissoryNotes
                .AsNoTracking()
                .Where(pn => pn.ReservationId.HasValue && keys.Contains(pn.ReservationId.Value))
                .ToListAsync(cancellationToken);

            var promissorySum = promissoryNotes
                .Where(n => !excludeNoteId.HasValue || n.PromissoryNoteId != excludeNoteId.Value)
                .Where(n => !ReservationFinancialSyncService.IsPromissoryNoteCancelled(n))
                .Sum(n => n.Amount);

            var totalAmount = reservation.TotalAmount ?? reservation.Subtotal ?? 0m;

            if (promissorySum + newOutstandingAmount > totalAmount + 0.01m)
            {
                throw new ArgumentException(
                    $"Promissory amount ({newOutstandingAmount:N2}) would exceed reservation total ({totalAmount:N2}) with existing notes ({promissorySum:N2}).");
            }
        }

        private static void EnsureEditable(PromissoryNote note)
        {
            var status = (note.Status ?? string.Empty).Trim().ToLowerInvariant();
            if (status is "collected" or "cancelled")
            {
                throw new ArgumentException("Collected or cancelled promissory notes cannot be modified.");
            }
        }

        private async Task EnsureNoteBelongsToReservationAsync(
            PromissoryNote note,
            int internalReservationId,
            CancellationToken cancellationToken)
        {
            var entity = await PmsReservationRouteResolver.FindAsync(
                _context,
                internalReservationId,
                cancellationToken: cancellationToken);

            if (entity == null)
            {
                throw new ArgumentException($"Reservation {internalReservationId} not found.");
            }

            var keys = ReservationFinancialSyncService.BuildReservationKeys(
                entity.ReservationId,
                entity.ZaaerId);

            if (!note.ReservationId.HasValue || !keys.Contains(note.ReservationId.Value))
            {
                throw new ArgumentException("Promissory note does not belong to this reservation.");
            }
        }

        private async Task<Reservation> LoadReservationAsync(int routeId, CancellationToken cancellationToken)
        {
            var reservation = await PmsReservationRouteResolver.FindAsync(
                _context,
                routeId,
                cancellationToken: cancellationToken);

            if (reservation == null)
            {
                throw new ArgumentException($"Reservation {routeId} not found.");
            }

            return reservation;
        }

        private static void EnsureHotelMatch(int dtoHotelId, int entityHotelId)
        {
            if (dtoHotelId != entityHotelId)
            {
                throw new ArgumentException("HotelId does not match.");
            }
        }

        /// <summary>
        /// Same storage rule as reservations: persist corporate <c>zaaer_id</c> when available.
        /// <paramref name="corporateRef"/> may be internal <c>corporate_id</c> or integration <c>zaaer_id</c>.
        /// </summary>
        private async Task<int?> ResolveCorporateStorageIdAsync(
            int hotelId,
            int? corporateRef,
            CancellationToken cancellationToken)
        {
            if (!corporateRef.HasValue || corporateRef.Value <= 0)
            {
                return null;
            }

            var corp = await _context.CorporateCustomers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.HotelId == hotelId &&
                         (c.CorporateId == corporateRef.Value || c.ZaaerId == corporateRef.Value),
                    cancellationToken);

            if (corp == null)
            {
                return corporateRef.Value;
            }

            return corp.ZaaerId is > 0 ? corp.ZaaerId.Value : corp.CorporateId;
        }

        private async Task<int> ResolveCustomerZaaerIdAsync(int internalCustomerId, CancellationToken cancellationToken)
        {
            var customerZaaerId = await _context.Customers
                .AsNoTracking()
                .Where(c => c.CustomerId == internalCustomerId)
                .Select(c => c.ZaaerId)
                .FirstOrDefaultAsync(cancellationToken);

            if (!customerZaaerId.HasValue || customerZaaerId.Value <= 0)
            {
                throw new ArgumentException("Customer ZaaerId is required for promissory notes.");
            }

            return customerZaaerId.Value;
        }

        private async Task<string?> ResolvePayableToAsync(
            string? dtoPayableTo,
            int internalCustomerId,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(dtoPayableTo))
            {
                return dtoPayableTo.Trim();
            }

            return await _context.Customers
                .AsNoTracking()
                .Where(c => c.CustomerId == internalCustomerId)
                .Select(c => c.CustomerName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private static string BuildDefaultReason(string reservationNo)
        {
            return string.IsNullOrWhiteSpace(reservationNo)
                ? "Rental fees for reservation"
                : $"Rental fees for reservation {reservationNo.Trim()}";
        }

        private async Task<Dictionary<int, string>> BuildCollectionReceiptNoLookupAsync(
            IReadOnlyList<int> collectionReceiptIds,
            CancellationToken cancellationToken)
        {
            var receipts = await _context.PaymentReceipts
                .AsNoTracking()
                .Where(r =>
                    collectionReceiptIds.Contains(r.ReceiptId)
                    || (r.ZaaerId.HasValue && collectionReceiptIds.Contains(r.ZaaerId.Value)))
                .Select(r => new { r.ReceiptId, r.ZaaerId, r.ReceiptNo })
                .ToListAsync(cancellationToken);

            var map = new Dictionary<int, string>();
            foreach (var id in collectionReceiptIds)
            {
                var match = receipts.FirstOrDefault(r => r.ZaaerId == id || r.ReceiptId == id);
                if (match != null)
                {
                    map[id] = match.ReceiptNo;
                }
            }

            return map;
        }

        private static PmsPromissoryNoteRowDto MapRow(
            PromissoryNote note,
            IReadOnlyDictionary<int, string>? receiptNos)
        {
            var due = Math.Max(0m, note.Amount - note.AmountCollected);
            string? collectionReceiptNo = null;
            if (note.CollectionReceiptId.HasValue &&
                receiptNos != null &&
                receiptNos.TryGetValue(note.CollectionReceiptId.Value, out var no))
            {
                collectionReceiptNo = no;
            }

            return new PmsPromissoryNoteRowDto
            {
                PromissoryNoteId = note.PromissoryNoteId,
                ZaaerId = note.ZaaerId,
                PromissoryNo = note.PromissoryNo,
                CreatedAt = note.CreatedAt,
                MaturityDate = note.MaturityDate,
                Amount = note.Amount,
                AmountCollected = note.AmountCollected,
                DueAmount = due,
                Status = note.Status,
                PayableTo = note.PayableTo,
                Reason = note.Reason,
                PlaceOfMaturity = note.PlaceOfMaturity,
                Notes = note.Notes,
                PaymentLinkSent = note.PaymentLinkSent,
                CollectionReceiptId = note.CollectionReceiptId,
                CollectionReceiptNo = collectionReceiptNo,
                HotelId = note.HotelId,
                ReservationId = note.ReservationId,
                CustomerId = note.CustomerId,
                CorporateId = note.CorporateId
            };
        }
    }
}
