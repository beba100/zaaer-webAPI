using FinanceLedgerAPI.Models;

using Microsoft.EntityFrameworkCore;

using zaaerIntegration.Data;

using zaaerIntegration.DTOs.Pms;

using zaaerIntegration.DTOs.Response;

using zaaerIntegration.Repositories.Interfaces;

using zaaerIntegration.Security;

using zaaerIntegration.Services.ActivityLog;

using zaaerIntegration.Services.Interfaces;

using zaaerIntegration.Utilities;



namespace zaaerIntegration.Services.Implementations

{

    /// <summary>

    /// PMS payment receipts: <c>payment_receipt</c> (REC) for receipts, <c>payment_refund</c> (PAY) for disbursements.

    /// </summary>

    public sealed class PmsPaymentReceiptService : IPmsPaymentReceiptService

    {

        private static readonly HashSet<string> AllowedReceiptTypes = new(StringComparer.OrdinalIgnoreCase)

        {

            "receipt",

            "security_deposit",

            "refund",

            "security_deposit_refund"

        };



        private readonly ApplicationDbContext _context;

        private readonly IUnitOfWork _unitOfWork;

        private readonly IPaymentReceiptRepository _paymentReceiptRepository;

        private readonly INumberingService _numberingService;

        private readonly ICurrentUserContext _currentUser;

        private readonly IPermissionService _permissions;

        private readonly IReservationActivityLogWriter _activityLog;

        private readonly IReservationFinancialSyncService _financialSync;

        private readonly IPmsCashLedgerService _cashLedger;



        public PmsPaymentReceiptService(

            ApplicationDbContext context,

            IUnitOfWork unitOfWork,

            IPaymentReceiptRepository paymentReceiptRepository,

            INumberingService numberingService,

            ICurrentUserContext currentUser,

            IPermissionService permissions,

            IReservationActivityLogWriter activityLog,

            IReservationFinancialSyncService financialSync,

            IPmsCashLedgerService cashLedger)

        {

            _context = context;

            _unitOfWork = unitOfWork;

            _paymentReceiptRepository = paymentReceiptRepository;

            _numberingService = numberingService;

            _currentUser = currentUser;

            _permissions = permissions;

            _activityLog = activityLog;

            _financialSync = financialSync;

            _cashLedger = cashLedger;

        }



        public async Task<IReadOnlyList<PmsPaymentReceiptRowDto>> ListByReservationAsync(

            int reservationId,

            string? receiptType = null,

            string? kind = null,

            CancellationToken cancellationToken = default)

        {

            var entity = await PmsReservationRouteResolver.FindAsync(
                _context,
                reservationId,
                cancellationToken: cancellationToken);

            if (entity == null)
            {
                return Array.Empty<PmsPaymentReceiptRowDto>();
            }

            var reservation = new { entity.ReservationId, entity.ZaaerId };



            var globalReservationId = reservation.ZaaerId;

            var query = _context.PaymentReceipts.AsNoTracking();



            if (globalReservationId.HasValue && globalReservationId.Value > 0)

            {

                var zid = globalReservationId.Value;

                query = query.Where(pr =>

                    pr.ReservationId == zid || pr.ReservationId == reservationId);

            }

            else

            {

                query = query.Where(pr => pr.ReservationId == reservationId);

            }



            if (!string.IsNullOrWhiteSpace(receiptType))

            {

                query = query.Where(pr => pr.ReceiptType == receiptType);

            }

            else if (!string.IsNullOrWhiteSpace(kind))

            {

                query = ApplyPaymentKindFilter(query, kind);

            }



            var rows = await (
                    from pr in query
                    join pm in _context.PaymentMethods.AsNoTracking()
                        on pr.PaymentMethodId equals pm.PaymentMethodId into pmJoin
                    from pm in pmJoin.DefaultIfEmpty()
                    orderby pr.ReceiptDate descending, pr.ReceiptId descending
                    select new PmsPaymentReceiptRowDto
                    {
                        ReceiptId = pr.ReceiptId,
                        ZaaerId = pr.ZaaerId,
                        ReceiptNo = pr.ReceiptNo,
                        ReceiptDate = pr.ReceiptDate,
                        AmountPaid = pr.AmountPaid,
                        PaymentMethodId = pr.PaymentMethodId,
                        PaymentMethod = pm != null ? pm.MethodName : pr.PaymentMethod,
                        VoucherCode = pr.VoucherCode,
                        ReceiptStatus = pr.ReceiptStatus,
                        ReceiptFrom = pr.ReceiptFrom,
                        ReceiptTo = pr.ReceiptTo,
                        ReceiptType = pr.ReceiptType,
                        Notes = pr.Notes,
                        Reason = pr.Reason,
                        BankId = pr.BankId,
                        TransactionNo = pr.TransactionNo,
                        HotelId = pr.HotelId,
                        ReservationId = pr.ReservationId,
                        CustomerId = pr.CustomerId,
                        UnitId = pr.UnitId,
                        OrderId = pr.OrderId,
                        IsBuildingGuardRent = pr.IsBuildingGuardRent
                    })
                .ToListAsync(cancellationToken);

            return rows.Select(NormalizeRowStatus).ToList();
        }

        public async Task<PmsPaymentReceiptRowDto?> GetByZaaerIdAsync(
            int zaaerId,
            CancellationToken cancellationToken = default)
        {
            if (zaaerId <= 0)
            {
                return null;
            }

            var row = await (
                    from pr in _context.PaymentReceipts.AsNoTracking()
                    where pr.ZaaerId == zaaerId
                    join pm in _context.PaymentMethods.AsNoTracking()
                        on pr.PaymentMethodId equals pm.PaymentMethodId into pmJoin
                    from pm in pmJoin.DefaultIfEmpty()
                    select new PmsPaymentReceiptRowDto
                    {
                        ReceiptId = pr.ReceiptId,
                        ZaaerId = pr.ZaaerId,
                        ReceiptNo = pr.ReceiptNo,
                        ReceiptDate = pr.ReceiptDate,
                        AmountPaid = pr.AmountPaid,
                        PaymentMethodId = pr.PaymentMethodId,
                        PaymentMethod = pm != null ? pm.MethodName : pr.PaymentMethod,
                        VoucherCode = pr.VoucherCode,
                        ReceiptStatus = pr.ReceiptStatus,
                        ReceiptFrom = pr.ReceiptFrom,
                        ReceiptTo = pr.ReceiptTo,
                        ReceiptType = pr.ReceiptType,
                        Notes = pr.Notes,
                        Reason = pr.Reason,
                        BankId = pr.BankId,
                        TransactionNo = pr.TransactionNo,
                        HotelId = pr.HotelId,
                        ReservationId = pr.ReservationId,
                        CustomerId = pr.CustomerId,
                        UnitId = pr.UnitId,
                        OrderId = pr.OrderId,
                        IsBuildingGuardRent = pr.IsBuildingGuardRent
                    })
                .FirstOrDefaultAsync(cancellationToken);

            return row == null ? null : NormalizeRowStatus(row);
        }

        public async Task<PmsLastRentReceiptDto?> GetLastRentReceiptAsync(
            int reservationId,
            CancellationToken cancellationToken = default)
        {
            var entity = await PmsReservationRouteResolver.FindAsync(
                _context,
                reservationId,
                cancellationToken: cancellationToken);

            if (entity == null)
            {
                return null;
            }

            var reservation = new { entity.ReservationId, entity.ZaaerId, entity.HotelId };
            var globalReservationId = reservation.ZaaerId;

            var query = _context.PaymentReceipts.AsNoTracking()
                .Where(pr => pr.HotelId == reservation.HotelId)
                .Where(pr =>
                    pr.VoucherCode == "receipt"
                    && pr.ReceiptFrom != null
                    && pr.ReceiptTo != null
                    && pr.ReceiptStatus != "cancelled");

            if (globalReservationId.HasValue && globalReservationId.Value > 0)
            {
                var zid = globalReservationId.Value;
                query = query.Where(pr => pr.ReservationId == zid || pr.ReservationId == reservationId);
            }
            else
            {
                query = query.Where(pr => pr.ReservationId == reservationId);
            }

            return await query
                .OrderByDescending(pr => pr.ReceiptTo)
                .ThenByDescending(pr => pr.ReceiptDate)
                .ThenByDescending(pr => pr.ReceiptId)
                .Select(pr => new PmsLastRentReceiptDto
                {
                    ReceiptNo = pr.ReceiptNo,
                    ReceiptDate = pr.ReceiptDate,
                    ReceiptFrom = pr.ReceiptFrom!.Value,
                    ReceiptTo = pr.ReceiptTo!.Value
                })
                .FirstOrDefaultAsync(cancellationToken);
        }



        public async Task<PaymentReceiptResponseDto> CreateAsync(

            PmsCreatePaymentReceiptDto dto,

            CancellationToken cancellationToken = default)

        {

            if (dto == null)

            {

                throw new ArgumentNullException(nameof(dto));

            }



            if (!AllowedReceiptTypes.Contains(dto.ReceiptType))

            {

                throw new ArgumentException(

                    "ReceiptType must be 'receipt', 'security_deposit', 'refund', or 'security_deposit_refund'.",

                    nameof(dto));

            }



            var reservation = await PmsReservationRouteResolver.FindAsync(
                _context,
                dto.ReservationId,
                cancellationToken: cancellationToken);

            if (reservation == null)
            {
                throw new ArgumentException($"Reservation {dto.ReservationId} not found.");
            }



            if (dto.HotelId != reservation.HotelId)

            {

                throw new ArgumentException("HotelId does not match the reservation.");

            }



            if (!reservation.ZaaerId.HasValue || reservation.ZaaerId.Value <= 0)

            {

                throw new ArgumentException("Reservation ZaaerId is required for payment receipts.");

            }



            var internalCustomerId = dto.CustomerId ?? reservation.CustomerId;

            if (!internalCustomerId.HasValue || internalCustomerId.Value <= 0)

            {

                throw new ArgumentException("CustomerId is required.");

            }



            var customerZaaerId = await _context.Customers

                .AsNoTracking()

                .Where(c => c.CustomerId == internalCustomerId!.Value || c.ZaaerId == internalCustomerId.Value)

                .Select(c => c.ZaaerId)

                .FirstOrDefaultAsync(cancellationToken);



            if (!customerZaaerId.HasValue || customerZaaerId.Value <= 0)

            {

                throw new ArgumentException("Customer ZaaerId is required for payment receipts.");

            }



            var promissoryNote = await ResolvePromissoryNoteForCollectionAsync(
                dto,
                reservation,
                cancellationToken);

            var storage = ClassifyReceiptStorage(dto.ReceiptType, dto.VoucherCode);
            var receiptType = storage.ReceiptType;
            var voucherCode = storage.VoucherCode;
            var isDisbursement = IsDisbursementType(receiptType);



            var auditIds = new List<long>();

            try

            {

                var documentCode = ResolveDocumentCode(receiptType);

                var pmsUserId = PmsCurrentUser.ResolveUserId(_currentUser, dto.CreatedBy);
                var identity = await AllocateUniquePaymentReceiptIdentityAsync(
                    documentCode,
                    dto.HotelId,
                    pmsUserId,
                    dto.ReservationId,
                    auditIds,
                    cancellationToken);



                string? paymentMethodName = null;

                if (dto.PaymentMethodId.HasValue && dto.PaymentMethodId.Value > 0)

                {

                    paymentMethodName = await _context.PaymentMethods

                        .AsNoTracking()

                        .Where(pm => pm.PaymentMethodId == dto.PaymentMethodId.Value)

                        .Select(pm => pm.MethodName)

                        .FirstOrDefaultAsync(cancellationToken);

                }



                var reason = BuildDefaultReason(voucherCode, reservation.ReservationNo, dto.Reason);

                var notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();

                DateTime? receiptFrom = null;

                DateTime? receiptTo = null;



                if (receiptType == "receipt" && voucherCode == "receipt")

                {

                    receiptFrom = dto.ReceiptFrom?.Date;

                    receiptTo = dto.ReceiptTo?.Date;

                    if (notes == null && receiptFrom.HasValue && receiptTo.HasValue)

                    {

                        notes = BuildRentPeriodNote(

                            receiptFrom,

                            receiptTo,

                            reservation.ReservationNo);

                    }

                }



                var amount = dto.AmountPaid;

                if (IsRefundVoucherCode(voucherCode) || isDisbursement)

                {

                    amount = amount > 0 ? -Math.Abs(amount) : (amount < 0 ? amount : 0m);

                }



                var receipt = new PaymentReceipt

                {

                    ReceiptNo = identity.DocumentNo,

                    ZaaerId = ZaaerIdMapper.ToNullableInt32(identity.ZaaerId),

                    HotelId = dto.HotelId,

                    ReservationId = reservation.ZaaerId.Value,

                    UnitId = null,

                    CustomerId = customerZaaerId.Value,

                    ReceiptDate = dto.ReceiptDate ?? KsaTime.Now,

                    ReceiptType = receiptType,

                    VoucherCode = voucherCode,

                    AmountPaid = amount,

                    PaymentMethodId = dto.PaymentMethodId > 0 ? dto.PaymentMethodId : null,

                    PaymentMethod = paymentMethodName,

                    BankId = dto.BankId > 0 ? dto.BankId : null,

                    TransactionNo = string.IsNullOrWhiteSpace(dto.TransactionNo)

                        ? string.Empty

                        : dto.TransactionNo.Trim(),

                    Reason = reason,

                    ReceiptFrom = receiptFrom,

                    ReceiptTo = receiptTo,

                    Notes = notes ?? string.Empty,

                    IsBuildingGuardRent = await ResolveBuildingGuardRentAsync(
                        dto.IsBuildingGuardRent,
                        voucherCode,
                        existing: null,
                        cancellationToken),

                    ReceiptStatus = "paid",

                    CreatedBy = pmsUserId,

                    CreatedAt = KsaTime.Now

                };



                var created = await _paymentReceiptRepository.AddAsync(receipt);

                await _unitOfWork.SaveChangesAsync();
                await _cashLedger.SyncPaymentReceiptAsync(created, cancellationToken);

                if (promissoryNote != null)
                {
                    await PmsPromissoryNoteService.ApplyCollectionAsync(
                        _context,
                        _unitOfWork,
                        promissoryNote,
                        created,
                        Math.Abs(amount),
                        cancellationToken);

                    await _activityLog.LogAsync(
                        new ReservationActivityLogEntry
                        {
                            EventKey = ReservationActivityEvents.PromissoryCollected,
                            HotelId = dto.HotelId,
                            ReservationId = reservation.ReservationId,
                            ReservationNo = reservation.ReservationNo,
                            RefType = "PromissoryNote",
                            RefId = promissoryNote.PromissoryNoteId,
                            RefNo = promissoryNote.PromissoryNo,
                            AmountTo = Math.Abs(amount),
                            IconKey = "money",
                            Payload = new Dictionary<string, object?>
                            {
                                ["promissoryNo"] = promissoryNote.PromissoryNo,
                                ["receiptNo"] = created.ReceiptNo,
                                ["amount"] = Math.Abs(amount)
                            },
                            ZaaerId = promissoryNote.ZaaerId
                        },
                        cancellationToken);
                }

                await _financialSync.SyncReservationRentPaymentTotalsAsync(
                    reservation.ReservationId,
                    cancellationToken);

                foreach (var auditId in auditIds)

                {

                    await _numberingService.MarkCommittedAsync(auditId, cancellationToken);

                }



                var eventKey = isDisbursement
                    ? ReservationActivityEvents.PaymentRefundCreated
                    : ReservationActivityEvents.PaymentReceiptCreated;

                await _activityLog.LogAsync(
                    new ReservationActivityLogEntry
                    {
                        EventKey = eventKey,
                        HotelId = dto.HotelId,
                        ReservationId = reservation.ReservationId,
                        ReservationNo = reservation.ReservationNo,
                        RefType = isDisbursement ? "PaymentRefund" : "PaymentReceipt",
                        RefId = created.ReceiptId,
                        RefNo = created.ReceiptNo,
                        AmountTo = amount,
                        IconKey = isDisbursement ? "undo" : "money",
                        Payload = new Dictionary<string, object?>
                        {
                            ["reservationNo"] = reservation.ReservationNo,
                            ["receiptNo"] = created.ReceiptNo,
                            ["amount"] = amount,
                            ["voucherCode"] = voucherCode
                        },
                        ZaaerId = created.ZaaerId
                    },
                    cancellationToken);



                return MapResponse(created, paymentMethodName);

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



        public async Task<PaymentReceiptResponseDto> UpdateByZaaerIdAsync(

            int zaaerId,

            PmsUpdatePaymentReceiptDto dto,

            CancellationToken cancellationToken = default)

        {

            if (dto == null)

            {

                throw new ArgumentNullException(nameof(dto));

            }



            if (zaaerId <= 0)

            {

                throw new ArgumentException("ZaaerId must be greater than zero.", nameof(zaaerId));

            }



            var receipt = await _context.PaymentReceipts

                .FirstOrDefaultAsync(pr => pr.ZaaerId == zaaerId, cancellationToken);



            if (receipt == null)

            {

                throw new ArgumentException($"Payment receipt with ZaaerId {zaaerId} not found.");

            }



            await EnsureReceiptBelongsToReservationAsync(receipt, dto.ReservationId, cancellationToken);



            if (receipt.HotelId != dto.HotelId)

            {

                throw new ArgumentException("HotelId does not match the receipt.");

            }



            if (IsReceiptCancelled(receipt))

            {

                throw new ArgumentException("Cancelled receipts cannot be edited.");

            }



            var storage = ClassifyReceiptStorage(
                dto.ReceiptType ?? receipt.ReceiptType ?? "receipt",
                dto.VoucherCode ?? receipt.VoucherCode);
            var receiptType = storage.ReceiptType;
            var voucherCode = storage.VoucherCode;
            var isDisbursement = IsDisbursementType(receiptType);

            if (isDisbursement && string.IsNullOrWhiteSpace(dto.Reason))
            {
                throw new ArgumentException("Reason is required for disbursement vouchers.", nameof(dto));
            }

            string? paymentMethodName = null;

            if (dto.PaymentMethodId.HasValue && dto.PaymentMethodId.Value > 0)

            {

                paymentMethodName = await _context.PaymentMethods

                    .AsNoTracking()

                    .Where(pm => pm.PaymentMethodId == dto.PaymentMethodId.Value)

                    .Select(pm => pm.MethodName)

                    .FirstOrDefaultAsync(cancellationToken);

            }



            var notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();

            DateTime? receiptFrom = null;

            DateTime? receiptTo = null;



            if (receiptType == "receipt" && voucherCode == "receipt")

            {

                receiptFrom = dto.ReceiptFrom?.Date;

                receiptTo = dto.ReceiptTo?.Date;

            }



            receipt.ReceiptType = receiptType;

            receipt.VoucherCode = voucherCode;

            var previousAmount = receipt.AmountPaid;

            var amount = Math.Abs(dto.AmountPaid);
            if (amount <= 0)
            {
                amount = Math.Abs(receipt.AmountPaid);
            }

            if (IsRefundVoucherCode(voucherCode) || isDisbursement)
            {
                amount = -amount;
            }

            receipt.AmountPaid = amount;

            receipt.ReceiptDate = dto.ReceiptDate ?? receipt.ReceiptDate;

            receipt.PaymentMethodId = dto.PaymentMethodId > 0 ? dto.PaymentMethodId : null;

            receipt.PaymentMethod = paymentMethodName;

            receipt.BankId = dto.BankId > 0 ? dto.BankId : null;

            receipt.TransactionNo = string.IsNullOrWhiteSpace(dto.TransactionNo)

                ? string.Empty

                : dto.TransactionNo.Trim();

            receipt.ReceiptFrom = receiptFrom;

            receipt.ReceiptTo = receiptTo;

            receipt.IsBuildingGuardRent = await ResolveBuildingGuardRentAsync(
                dto.IsBuildingGuardRent,
                voucherCode,
                receipt,
                cancellationToken);

            receipt.Notes = notes ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(dto.Reason))
            {
                receipt.Reason = dto.Reason.Trim();
            }

            receipt.UnitId = null;



            await _unitOfWork.SaveChangesAsync();
            await _cashLedger.SyncPaymentReceiptAsync(receipt, cancellationToken);

            var internalReservationId = await ResolveInternalReservationIdAsync(dto.ReservationId, cancellationToken);

            if (receiptType.Equals("receipt", StringComparison.OrdinalIgnoreCase) ||
                receiptType.Equals("refund", StringComparison.OrdinalIgnoreCase))
            {
                await _financialSync.SyncReservationRentPaymentTotalsAsync(internalReservationId, cancellationToken);
            }

            var reservationRow = await _context.Reservations.AsNoTracking()
                .Where(r => r.ReservationId == internalReservationId)
                .Select(r => new { r.ReservationId, r.ReservationNo, r.HotelId })
                .FirstOrDefaultAsync(cancellationToken);

            if (reservationRow != null && Math.Round(previousAmount, 2) != Math.Round(amount, 2))
            {
                var updateEventKey = isDisbursement
                    ? ReservationActivityEvents.PaymentRefundUpdated
                    : ReservationActivityEvents.PaymentReceiptUpdated;

                await _activityLog.LogAsync(
                    new ReservationActivityLogEntry
                    {
                        EventKey = updateEventKey,
                        HotelId = reservationRow.HotelId,
                        ReservationId = reservationRow.ReservationId,
                        ReservationNo = reservationRow.ReservationNo,
                        RefType = isDisbursement ? "PaymentRefund" : "PaymentReceipt",
                        RefId = receipt.ReceiptId,
                        RefNo = receipt.ReceiptNo,
                        AmountFrom = previousAmount,
                        AmountTo = amount,
                        IconKey = "edit",
                        Payload = new Dictionary<string, object?>
                        {
                            ["reservationNo"] = reservationRow.ReservationNo,
                            ["receiptNo"] = receipt.ReceiptNo,
                            ["amountFrom"] = previousAmount,
                            ["amountTo"] = amount,
                            ["voucherCode"] = voucherCode
                        },
                        ZaaerId = receipt.ZaaerId
                    },
                    cancellationToken);
            }

            return MapResponse(receipt, paymentMethodName);

        }



        public async Task<PaymentReceiptResponseDto> CancelByZaaerIdAsync(

            int zaaerId,

            PmsCancelPaymentReceiptDto dto,

            CancellationToken cancellationToken = default)

        {

            if (dto == null)

            {

                throw new ArgumentNullException(nameof(dto));

            }



            if (zaaerId <= 0)

            {

                throw new ArgumentException("ZaaerId must be greater than zero.", nameof(zaaerId));

            }



            if (string.IsNullOrWhiteSpace(dto.Reason))

            {

                throw new ArgumentException("Cancellation reason is required.", nameof(dto));

            }



            var receipt = await _context.PaymentReceipts

                .FirstOrDefaultAsync(pr => pr.ZaaerId == zaaerId, cancellationToken);



            if (receipt == null)

            {

                throw new ArgumentException($"Payment receipt with ZaaerId {zaaerId} not found.");

            }



            await EnsureReceiptBelongsToReservationAsync(receipt, dto.ReservationId, cancellationToken);



            if (receipt.HotelId != dto.HotelId)

            {

                throw new ArgumentException("HotelId does not match the receipt.");

            }



            if (IsReceiptCancelled(receipt))

            {

                throw new ArgumentException("Receipt is already cancelled.");

            }



            await EnsureCanCancelReceiptAsync(receipt, cancellationToken);



            receipt.ReceiptStatus = "cancelled";

            receipt.Reason = dto.Reason.Trim();



            await _unitOfWork.SaveChangesAsync();
            await _cashLedger.SyncPaymentReceiptAsync(receipt, cancellationToken);

            var internalReservationId = await ResolveInternalReservationIdAsync(dto.ReservationId, cancellationToken);
            await _financialSync.SyncReservationRentPaymentTotalsAsync(internalReservationId, cancellationToken);

            string? paymentMethodName = null;

            if (receipt.PaymentMethodId.HasValue && receipt.PaymentMethodId.Value > 0)

            {

                paymentMethodName = await _context.PaymentMethods

                    .AsNoTracking()

                    .Where(pm => pm.PaymentMethodId == receipt.PaymentMethodId.Value)

                    .Select(pm => pm.MethodName)

                    .FirstOrDefaultAsync(cancellationToken);

            }



            return MapResponse(receipt, paymentMethodName);

        }



        private async Task<PromissoryNote?> ResolvePromissoryNoteForCollectionAsync(
            PmsCreatePaymentReceiptDto dto,
            Reservation reservation,
            CancellationToken cancellationToken)
        {
            if (!dto.PromissoryNoteZaaerId.HasValue || dto.PromissoryNoteZaaerId.Value <= 0)
            {
                return null;
            }

            var note = await _context.PromissoryNotes
                .FirstOrDefaultAsync(pn => pn.ZaaerId == dto.PromissoryNoteZaaerId.Value, cancellationToken);

            if (note == null)
            {
                throw new ArgumentException(
                    $"Promissory note with ZaaerId {dto.PromissoryNoteZaaerId.Value} not found.");
            }

            if (note.HotelId != dto.HotelId)
            {
                throw new ArgumentException("HotelId does not match the promissory note.");
            }

            var keys = ReservationFinancialSyncService.BuildReservationKeys(
                reservation.ReservationId,
                reservation.ZaaerId);

            if (!note.ReservationId.HasValue || !keys.Contains(note.ReservationId.Value))
            {
                throw new ArgumentException("Promissory note does not belong to this reservation.");
            }

            var status = (note.Status ?? string.Empty).Trim().ToLowerInvariant();
            if (status is "collected" or "cancelled")
            {
                throw new ArgumentException("Promissory note is not eligible for collection.");
            }

            var due = ReservationFinancialSyncService.GetPromissoryOutstanding(note);
            if (dto.AmountPaid <= 0)
            {
                throw new ArgumentException("Collection amount must be greater than zero.");
            }

            if (dto.AmountPaid > due + 0.01m)
            {
                throw new ArgumentException(
                    $"Collection amount ({dto.AmountPaid:N2}) exceeds promissory due amount ({due:N2}).");
            }

            if (!dto.ReceiptFrom.HasValue || !dto.ReceiptTo.HasValue)
            {
                throw new ArgumentException(
                    "Receipt period (receipt_from / receipt_to) is required when collecting a promissory note.");
            }

            return note;
        }

        private async Task EnsureReceiptBelongsToReservationAsync(

            PaymentReceipt receipt,

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

            var reservation = new { entity.ReservationId, entity.ZaaerId };
            internalReservationId = entity.ReservationId;



            var matchesInternal = receipt.ReservationId == internalReservationId;

            var matchesGlobal = reservation.ZaaerId.HasValue &&

                                receipt.ReservationId == reservation.ZaaerId.Value;



            if (!matchesInternal && !matchesGlobal)

            {

                throw new ArgumentException("ReservationId does not match the receipt.");

            }

        }



        private static IQueryable<PaymentReceipt> ApplyPaymentKindFilter(

            IQueryable<PaymentReceipt> query,

            string kind)

        {

            if (kind.Equals("disbursements", StringComparison.OrdinalIgnoreCase))

            {

                return query.Where(pr =>

                    pr.ReceiptType == "refund" ||

                    pr.ReceiptType == "security_deposit_refund" ||

                    pr.ReceiptType == "expense" ||

                    pr.AmountPaid < 0);

            }



            return query.Where(pr =>

                pr.ReceiptType == "receipt" ||

                pr.ReceiptType == "security_deposit");

        }



        private static bool IsRefundVoucherCode(string? voucherCode) =>
            voucherCode != null && (
                voucherCode.Equals("refund", StringComparison.OrdinalIgnoreCase)
                || voucherCode.Equals("security_deposit_refund", StringComparison.OrdinalIgnoreCase));

        private static bool IsDisbursementType(string receiptType) =>

            receiptType.Equals("refund", StringComparison.OrdinalIgnoreCase) ||

            receiptType.Equals("security_deposit_refund", StringComparison.OrdinalIgnoreCase) ||

            receiptType.Equals("expense", StringComparison.OrdinalIgnoreCase);



        private async Task EnsureCanCancelReceiptAsync(PaymentReceipt receipt, CancellationToken cancellationToken)

        {

            if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue || !_currentUser.TenantId.HasValue)

            {

                throw new ReservationPermissionDeniedException("payments.cancel");

            }



            var storage = ClassifyReceiptStorage(receipt.ReceiptType ?? string.Empty, receipt.VoucherCode);
            var type = storage.ReceiptType;

            var permissionCode = IsDisbursementType(type)

                ? "payments.refund_voucher.cancel"

                : "payments.cancel";



            var allowed = await _permissions.HasPermissionAsync(

                _currentUser.UserId.Value,

                _currentUser.TenantId.Value,

                permissionCode,

                _currentUser.AuthMode,

                cancellationToken);



            if (!allowed)

            {

                throw new ReservationPermissionDeniedException(permissionCode);

            }

        }



        private sealed record ReceiptStorageFields(string ReceiptType, string VoucherCode);

        /// <summary>
        /// Maps API/legacy input to DB columns: <c>receipt_type</c> (receipt|refund) + <c>voucher_code</c>.
        /// </summary>
        private static ReceiptStorageFields ClassifyReceiptStorage(string? receiptType, string? voucherCode)
        {
            var type = (receiptType ?? string.Empty).Trim();
            var voucher = (voucherCode ?? string.Empty).Trim();

            if (voucher.Equals("security_deposit", StringComparison.OrdinalIgnoreCase)
                || type.Equals("security_deposit", StringComparison.OrdinalIgnoreCase))
            {
                return new ReceiptStorageFields("receipt", "security_deposit");
            }

            if (voucher.Equals("security_deposit_refund", StringComparison.OrdinalIgnoreCase)
                || type.Equals("security_deposit_refund", StringComparison.OrdinalIgnoreCase))
            {
                return new ReceiptStorageFields("refund", "security_deposit_refund");
            }

            if (type.Equals("refund", StringComparison.OrdinalIgnoreCase)
                || type.Equals("expense", StringComparison.OrdinalIgnoreCase)
                || voucher.Equals("refund", StringComparison.OrdinalIgnoreCase))
            {
                return new ReceiptStorageFields("refund", "refund");
            }

            if (!string.IsNullOrWhiteSpace(voucher)
                && !voucher.Equals("receipt", StringComparison.OrdinalIgnoreCase))
            {
                return new ReceiptStorageFields("receipt", voucher);
            }

            return new ReceiptStorageFields("receipt", "receipt");
        }

        private static string ResolveDocumentCode(string receiptType) =>
            IsDisbursementType(receiptType) ? "payment_refund" : "payment_receipt";



        private static bool IsReceiptCancelled(PaymentReceipt receipt) =>

            receipt.ReceiptStatus?.Trim().Equals("cancelled", StringComparison.OrdinalIgnoreCase) == true;



        private static string? BuildDefaultReason(

            string voucherCode,

            string? reservationNo,

            string? dtoReason)

        {

            if (!string.IsNullOrWhiteSpace(dtoReason))

            {

                return dtoReason.Trim();

            }



            var no = string.IsNullOrWhiteSpace(reservationNo) ? "" : reservationNo.Trim();

            return voucherCode switch

            {

                "security_deposit" => $"Security deposit for reservation no.: {no}",

                "security_deposit_refund" => $"Security deposit refund for reservation no.: {no}",

                "refund" => $"Refund for reservation no.: {no}",

                _ => $"Rental fees for reservation no.: {no}"

            };

        }



        private static string BuildRentPeriodNote(

            DateTime? receiptFrom,

            DateTime? receiptTo,

            string? reservationNo)

        {

            var from = receiptFrom?.ToString("yyyy-MM-dd") ?? "";

            var to = receiptTo?.ToString("yyyy-MM-dd") ?? "";

            var no = string.IsNullOrWhiteSpace(reservationNo) ? "" : reservationNo.Trim();

            return $"Rent from {from} to {to} for reservation #{no}";

        }



        private static PmsPaymentReceiptRowDto NormalizeRowStatus(PmsPaymentReceiptRowDto row)

        {

            row.ReceiptStatus = NormalizeReceiptStatus(row.ReceiptStatus);

            var storage = ClassifyReceiptStorage(row.ReceiptType, row.VoucherCode);
            row.ReceiptType = storage.ReceiptType;
            row.VoucherCode = storage.VoucherCode;



            return row;

        }



        private static string NormalizeReceiptStatus(string? status)

        {

            var s = status?.Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(s) || s == "active")

            {

                return "paid";

            }



            return s;

        }



        private static PaymentReceiptResponseDto MapResponse(PaymentReceipt pr, string? paymentMethodName)

        {

            return new PaymentReceiptResponseDto

            {

                ReceiptId = pr.ReceiptId,

                ReceiptNo = pr.ReceiptNo,

                HotelId = pr.HotelId,

                ReservationId = pr.ReservationId,

                UnitId = pr.UnitId,

                InvoiceId = pr.InvoiceId,

                CustomerId = pr.CustomerId ?? 0,

                ReceiptDate = pr.ReceiptDate,

                ReceiptType = pr.ReceiptType,

                AmountPaid = pr.AmountPaid,

                PaymentMethodId = pr.PaymentMethodId,

                PaymentMethod = paymentMethodName ?? pr.PaymentMethod,

                BankId = pr.BankId,

                TransactionNo = pr.TransactionNo,

                Notes = pr.Notes,

                CreatedBy = pr.CreatedBy,

                CreatedAt = pr.CreatedAt,

                PaymentMethodName = paymentMethodName ?? pr.PaymentMethod

            };

        }

        private async Task<int> ResolveInternalReservationIdAsync(
            int routeId,
            CancellationToken cancellationToken)
        {
            var reservation = await PmsReservationRouteResolver.FindAsync(
                _context,
                routeId,
                cancellationToken: cancellationToken);

            if (reservation == null)
            {
                throw new ArgumentException($"Reservation {routeId} not found.");
            }

            return reservation.ReservationId;
        }

        private async Task<bool> ResolveBuildingGuardRentAsync(
            bool requested,
            string voucherCode,
            PaymentReceipt? existing,
            CancellationToken cancellationToken)
        {
            if (!string.Equals(voucherCode, "receipt", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue || !_currentUser.TenantId.HasValue)
            {
                return existing?.IsBuildingGuardRent ?? false;
            }

            var allowed = await _permissions.HasPermissionAsync(
                _currentUser.UserId.Value,
                _currentUser.TenantId.Value,
                "payments.building_guard_rent",
                _currentUser.AuthMode,
                cancellationToken);

            if (!allowed)
            {
                return existing?.IsBuildingGuardRent ?? false;
            }

            return requested;
        }

        private async Task<GeneratedBusinessIdentity> AllocateUniquePaymentReceiptIdentityAsync(
            string documentCode,
            int hotelId,
            int? pmsUserId,
            int reservationRouteId,
            List<long> auditIds,
            CancellationToken cancellationToken)
        {
            const int maxAttempts = 4;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var identity = await _numberingService.GetNextBusinessIdentityAsync(
                    documentCode,
                    hotelId,
                    pmsUserId?.ToString() ?? "pms",
                    $"pms-payment-receipt:{hotelId}:{reservationRouteId}:{Guid.NewGuid():N}",
                    cancellationToken);

                auditIds.Add(identity.AuditId);

                var zaaerId = ZaaerIdMapper.ToNullableInt32(identity.ZaaerId);
                var duplicateZaaer = zaaerId is > 0
                    && await _context.PaymentReceipts.AsNoTracking()
                        .AnyAsync(pr => pr.ZaaerId == zaaerId, cancellationToken);
                var duplicateReceiptNo = await _context.PaymentReceipts.AsNoTracking()
                    .AnyAsync(
                        pr => pr.HotelId == hotelId && pr.ReceiptNo == identity.DocumentNo,
                        cancellationToken);

                if (!duplicateZaaer && !duplicateReceiptNo)
                {
                    return identity;
                }

                await _numberingService.MarkVoidedAsync(
                    identity.AuditId,
                    duplicateZaaer
                        ? "Duplicate payment receipt zaaer_id"
                        : "Duplicate payment receipt number",
                    cancellationToken);
                auditIds.Remove(identity.AuditId);
            }

            throw new InvalidOperationException(
                "Could not allocate a unique payment receipt number. Please retry or contact support.");
        }

    }

}


