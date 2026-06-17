using System.Drawing;
using DevExpress.Drawing.Printing;
using DevExpress.XtraPrinting;
using DevExpress.XtraPrinting.BarCode;
using DevExpress.XtraReports.UI;
using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Reporting.Abstractions;
using zaaerIntegration.Services.Integrations.Zatca;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    public sealed class PmsResortTicketService : IPmsResortTicketService
    {
        public const string InvoiceTypeTicketSale = "ticket_sale";
        private const string StatusActive = "active";
        private const string StatusCancelled = "cancelled";
        private const string TicketStatusIssued = "issued";
        private const string TicketStatusPrinted = "printed";
        private const string TicketStatusUsed = "used";
        private const string PaymentStatusPaid = "paid";
        private const string PaymentStatusRefunded = "refunded";
        private const string PaymentStatusUnpaid = "unpaid";
        private const string ReceiptStatusPaid = "paid";

        private readonly ApplicationDbContext _context;
        private readonly ITenantService _tenantService;
        private readonly INumberingService _numberingService;
        private readonly ICurrentUserContext _currentUser;
        private readonly IReportRenderService _renderService;
        private readonly IReportAssetCache _assetCache;
        private readonly ResortTicketQrSecurity _qrSecurity;

        public PmsResortTicketService(
            ApplicationDbContext context,
            ITenantService tenantService,
            INumberingService numberingService,
            ICurrentUserContext currentUser,
            IReportRenderService renderService,
            IReportAssetCache assetCache,
            ResortTicketQrSecurity qrSecurity)
        {
            _context = context;
            _tenantService = tenantService;
            _numberingService = numberingService;
            _currentUser = currentUser;
            _renderService = renderService;
            _assetCache = assetCache;
            _qrSecurity = qrSecurity;
        }

        public async Task<IReadOnlyList<PmsResortTicketTypeDto>> ListTicketTypesAsync(string? category = null, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentResortScopeAsync(cancellationToken);
            var query = _context.ResortTicketTypes.AsNoTracking()
                .Where(t => t.HotelId == scope.ScopeHotelId || t.HotelId == scope.LocalHotelId);

            var normalizedCategory = category?.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(normalizedCategory))
            {
                query = query.Where(t => t.TicketCategory == normalizedCategory);
            }

            var rows = await query
                .OrderBy(t => t.TicketCategory)
                .ThenBy(t => t.SortOrder)
                .ThenByDescending(t => t.IsGeneric)
                .ThenBy(t => t.Code)
                .ToListAsync(cancellationToken);

            return rows.Select(MapTicketType).ToList();
        }

        public async Task<PmsResortTicketTypeDto?> GetTicketTypeAsync(int id, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentResortScopeAsync(cancellationToken);
            var entity = await _context.ResortTicketTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => (t.HotelId == scope.ScopeHotelId || t.HotelId == scope.LocalHotelId) && (t.TicketTypeId == id || t.ZaaerId == id), cancellationToken);
            return entity == null ? null : MapTicketType(entity);
        }

        public async Task<PmsResortTicketTypeDto> CreateTicketTypeAsync(PmsUpsertResortTicketTypeDto dto, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentResortScopeAsync(cancellationToken);
            var taxConfig = await ResolveTicketTaxConfigAsync(scope, cancellationToken);
            var zaaer = await _numberingService.GetNextEntityZaaerIdAsync(
                NumberingDocCodes.ResortTicketType,
                "pms-resort-ticket-type",
                $"resort-ticket-type:{scope.ScopeHotelId}:{Guid.NewGuid():N}",
                cancellationToken);

            try
            {
                var entity = new ResortTicketType
                {
                    HotelId = scope.ScopeHotelId,
                    Code = NormalizeCode(dto.Code),
                    NameAr = dto.NameAr.Trim(),
                    NameEn = dto.NameEn?.Trim(),
                    Description = dto.Description?.Trim(),
                    UnitPrice = dto.UnitPrice,
                    VatRate = taxConfig.VatRate,
                    ValidForHours = dto.ValidForHours,
                    ValidForMinutes = ResolveValidForMinutesFromDto(dto),
                    ValidityMode = ResolveValidityModeFromDto(dto),
                    TicketCategory = ResortTicketCategories.Normalize(dto.TicketCategory),
                    SortOrder = dto.SortOrder,
                    IsGeneric = dto.IsGeneric,
                    IsActive = dto.IsActive,
                    ZaaerId = ToInt32(zaaer.ZaaerId),
                    CreatedAt = KsaTime.Now
                };

                _context.ResortTicketTypes.Add(entity);
                await _context.SaveChangesAsync(cancellationToken);
                await _numberingService.MarkCommittedAsync(zaaer.AuditId, cancellationToken);
                return MapTicketType(entity);
            }
            catch (Exception ex)
            {
                await _numberingService.MarkVoidedAsync(zaaer.AuditId, ex.Message, cancellationToken);
                throw;
            }
        }

        public async Task<PmsResortTicketTypeDto?> UpdateTicketTypeAsync(int id, PmsUpsertResortTicketTypeDto dto, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentResortScopeAsync(cancellationToken);
            var taxConfig = await ResolveTicketTaxConfigAsync(scope, cancellationToken);
            var entity = await _context.ResortTicketTypes
                .FirstOrDefaultAsync(t => (t.HotelId == scope.ScopeHotelId || t.HotelId == scope.LocalHotelId) && (t.TicketTypeId == id || t.ZaaerId == id), cancellationToken);
            if (entity == null)
            {
                return null;
            }

            entity.Code = NormalizeCode(dto.Code);
            entity.NameAr = dto.NameAr.Trim();
            entity.NameEn = dto.NameEn?.Trim();
            entity.Description = dto.Description?.Trim();
            entity.UnitPrice = dto.UnitPrice;
            entity.VatRate = taxConfig.VatRate;
            entity.ValidForHours = dto.ValidForHours;
            entity.ValidForMinutes = ResolveValidForMinutesFromDto(dto);
            entity.ValidityMode = ResolveValidityModeFromDto(dto);
            entity.TicketCategory = ResortTicketCategories.Normalize(dto.TicketCategory);
            entity.SortOrder = dto.SortOrder;
            entity.IsGeneric = dto.IsGeneric;
            entity.IsActive = dto.IsActive;
            entity.UpdatedAt = KsaTime.Now;

            await _context.SaveChangesAsync(cancellationToken);
            return MapTicketType(entity);
        }

        public async Task<PmsResortTicketTypeDto?> SetTicketTypeActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentResortScopeAsync(cancellationToken);
            var entity = await _context.ResortTicketTypes
                .FirstOrDefaultAsync(t => (t.HotelId == scope.ScopeHotelId || t.HotelId == scope.LocalHotelId) && (t.TicketTypeId == id || t.ZaaerId == id), cancellationToken);
            if (entity == null)
            {
                return null;
            }

            entity.IsActive = isActive;
            entity.UpdatedAt = KsaTime.Now;
            await _context.SaveChangesAsync(cancellationToken);
            return MapTicketType(entity);
        }

        public async Task<PmsResortTicketLookupsDto> GetLookupsAsync(CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentResortScopeAsync(cancellationToken);
            var taxConfig = await ResolveTicketTaxConfigAsync(scope, cancellationToken);

            var paymentMethods = await _context.PaymentMethods
                .AsNoTracking()
                .Where(pm => pm.IsActive)
                .OrderBy(pm => pm.SortOrder)
                .ThenBy(pm => pm.MethodName)
                .Select(pm => new PmsResortTicketPaymentMethodDto
                {
                    Id = pm.PaymentMethodId,
                    Name = pm.MethodName,
                    NameAr = pm.MethodNameAr,
                    Code = pm.MethodCode
                })
                .ToListAsync(cancellationToken);

            var banks = await _context.Banks
                .AsNoTracking()
                .Where(b => b.IsActive && b.ZaaerId.HasValue && b.ZaaerId.Value > 0)
                .OrderByDescending(b => b.IsDefault)
                .ThenBy(b => b.SortOrder)
                .ThenBy(b => b.BankNameAr)
                .Select(b => new PmsResortTicketBankDto
                {
                    Id = b.ZaaerId!.Value,
                    Name = b.BankNameEn ?? b.BankNameAr ?? b.BankCode ?? string.Empty,
                    NameAr = b.BankNameAr
                })
                .ToListAsync(cancellationToken);

            var businessConfig = await GetOrCreateConfigEntityAsync(scope, cancellationToken);
            var now = KsaTime.Now;
            var businessDto = MapBusinessConfigDto(businessConfig, now);

            return new PmsResortTicketLookupsDto
            {
                IsResort = true,
                TicketCategories = ResortTicketCategories.All
                    .Select(c => new PmsResortTicketLookupItemDto { Id = c, Name = c })
                    .ToList(),
                OrderStatuses = new[]
                {
                    new PmsResortTicketLookupItemDto { Id = StatusActive, Name = StatusActive },
                    new PmsResortTicketLookupItemDto { Id = StatusCancelled, Name = StatusCancelled }
                },
                PaymentStatuses = new[]
                {
                    new PmsResortTicketLookupItemDto { Id = "paid", Name = "paid" },
                    new PmsResortTicketLookupItemDto { Id = "unpaid", Name = "unpaid" },
                    new PmsResortTicketLookupItemDto { Id = "refund_required", Name = "refund_required" },
                    new PmsResortTicketLookupItemDto { Id = "cancelled", Name = "cancelled" }
                },
                PaymentMethods = paymentMethods,
                Banks = banks,
                PricingTax = new PmsResortTicketPricingTaxDto
                {
                    VatRate = taxConfig.VatRate,
                    VatTaxIncluded = taxConfig.VatIncluded
                },
                BusinessConfig = businessDto
            };
        }

        public async Task<IReadOnlyList<PmsResortTicketOrderDto>> ListOrdersAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? reservationId = null,
            string? paymentStatus = null,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentResortScopeAsync(cancellationToken);
            var query = _context.ResortTicketOrders.AsNoTracking()
                .Where(o => o.HotelId == scope.ScopeHotelId || o.HotelId == scope.LocalHotelId);

            if (fromDate.HasValue)
            {
                var from = NormalizeQueryDate(fromDate)!.Value;
                query = query.Where(o => o.ServiceDate >= from);
            }

            if (toDate.HasValue)
            {
                var to = NormalizeQueryDate(toDate)!.Value;
                query = query.Where(o => o.ServiceDate <= to);
            }

            if (reservationId.HasValue)
            {
                query = query.Where(o => o.ReservationId == reservationId.Value);
            }

            if (!string.IsNullOrWhiteSpace(paymentStatus))
            {
                var status = paymentStatus.Trim().ToLowerInvariant();
                query = query.Where(o => o.PaymentStatus != null && o.PaymentStatus.ToLower() == status);
            }

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .ThenByDescending(o => o.TicketOrderId)
                .ToListAsync(cancellationToken);

            return await MapOrdersAsync(orders, cancellationToken);
        }

        public async Task<PmsResortTicketOrderDto?> GetOrderAsync(int id, CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentResortScopeAsync(cancellationToken);
            var order = await FindOrderByTicketOrderIdAsync(scope, id, tracking: false, cancellationToken);
            if (order == null)
            {
                return null;
            }

            var dto = (await MapOrdersAsync(new[] { order }, cancellationToken))[0];
            dto.Financial = await BuildOrderFinancialAsync(order, dto.Tickets, cancellationToken);
            return dto;
        }

        public async Task<PmsResortTicketOrderDto> CreateOrderAsync(PmsCreateResortTicketOrderDto dto, CancellationToken cancellationToken = default)
        {
            if (dto.Lines.Count == 0)
            {
                throw new ArgumentException("At least one ticket line is required.");
            }

            var scope = await GetCurrentResortScopeAsync(cancellationToken);
            var config = await GetOrCreateConfigEntityAsync(scope, cancellationToken);
            var now = KsaTime.Now;
            if (!ResortTicketBusinessHours.IsWithinIssueWindow(now, config.IssueStartTime, config.DailyCloseTime))
            {
                throw new InvalidOperationException("Ticket issuance is outside the allowed business hours.");
            }

            var businessServiceDate = ResortTicketBusinessHours.ResolveCurrentBusinessServiceDate(
                now,
                config.IssueStartTime,
                config.DailyCloseTime);
            var serviceDate = dto.ServiceDate?.Date ?? businessServiceDate ?? now.Date;
            var typeIds = dto.Lines.Select(l => l.TicketTypeId).Distinct().ToList();
            var ticketTypes = await _context.ResortTicketTypes
                .Where(t => (t.HotelId == scope.ScopeHotelId || t.HotelId == scope.LocalHotelId) && typeIds.Contains(t.TicketTypeId) && t.IsActive)
                .ToListAsync(cancellationToken);

            if (ticketTypes.Count != typeIds.Count)
            {
                throw new ArgumentException("One or more ticket types are inactive or not found.");
            }

            if (dto.PayNow && !dto.PaymentMethodId.HasValue)
            {
                throw new ArgumentException("Payment method is required when pay now is enabled.");
            }

            PaymentMethod? paymentMethod = null;
            if (dto.PayNow)
            {
                paymentMethod = await ResolvePaymentMethodAsync(dto.PaymentMethodId!.Value, cancellationToken);
                if (!IsCashPaymentMethod(paymentMethod) && !dto.BankId.HasValue)
                {
                    dto.BankId = await ResolveDefaultBankZaaerIdAsync(cancellationToken);
                }

                if (!IsCashPaymentMethod(paymentMethod) && !dto.BankId.HasValue)
                {
                    throw new ArgumentException("Bank is required for non-cash payment. Configure a default bank (is_default = 1).");
                }
            }

            var taxConfig = await ResolveTicketTaxConfigAsync(scope, cancellationToken);

            var reservationKey = await ResolveReservationKeyAsync(dto.ReservationId, cancellationToken);
            var orderIdentity = await _numberingService.GetNextBusinessIdentityAsync(
                NumberingDocCodes.ResortTicketOrder,
                scope.ScopeHotelId,
                "pms-resort-ticket-order",
                $"resort-ticket-order:{scope.ScopeHotelId}:{Guid.NewGuid():N}",
                cancellationToken);

            GeneratedBusinessIdentity? receiptIdentity = null;
            if (dto.PayNow)
            {
                receiptIdentity = await _numberingService.GetNextBusinessIdentityAsync(
                    NumberingDocCodes.PaymentReceipt,
                    scope.ScopeHotelId,
                    "pms-resort-ticket-receipt",
                    $"resort-ticket-receipt:{orderIdentity.DocumentNo}",
                    cancellationToken);
            }

            var auditIds = new List<long> { orderIdentity.AuditId };
            if (receiptIdentity != null)
            {
                auditIds.Add(receiptIdentity.AuditId);
            }

            var hotel = await LoadHotelAsync(scope, cancellationToken);

            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var tickets = new List<ResortTicket>();
                foreach (var line in dto.Lines)
                {
                    var type = ticketTypes.Single(t => t.TicketTypeId == line.TicketTypeId);
                    for (var i = 0; i < line.Quantity; i++)
                    {
                        var ticketZaaer = await _numberingService.GetNextEntityZaaerIdAsync(
                            NumberingDocCodes.ResortTicket,
                            "pms-resort-ticket",
                            $"resort-ticket:{orderIdentity.DocumentNo}:{line.TicketTypeId}:{i}:{Guid.NewGuid():N}",
                            cancellationToken);
                        auditIds.Add(ticketZaaer.AuditId);

                        var calc = HotelPricingTaxHelper.CalculateAmounts(type.UnitPrice, taxConfig);
                        var zaaerId = ToInt32(ticketZaaer.ZaaerId);
                        var validFrom = ResortTicketBusinessHours.ComputeValidFrom(serviceDate, config.IssueStartTime, now);
                        var validForMinutes = ResolveValidForMinutes(type);
                        DateTime validTo;
                        if (ResortTicketValidityModes.IsFromFirstScan(type.ValidityMode))
                        {
                            validTo = ResortTicketBusinessHours.ComputePreActivationValidTo(
                                serviceDate,
                                type.TicketCategory,
                                config.IssueStartTime,
                                config.TicketValidityEndTime,
                                config.GamesValidityEndTime);
                        }
                        else
                        {
                            validTo = ResortTicketBusinessHours.ComputeValidTo(
                                serviceDate,
                                type.TicketCategory,
                                config.IssueStartTime,
                                config.TicketValidityEndTime,
                                config.GamesValidityEndTime,
                                validForMinutes,
                                validFrom);
                        }

                        tickets.Add(new ResortTicket
                        {
                            HotelId = scope.ScopeHotelId,
                            TicketTypeId = type.TicketTypeId,
                            TicketNo = ResortTicketQrSecurity.BuildTicketNumber(zaaerId, hotel.HotelCode),
                            QrCode = _qrSecurity.BuildSignedQrCode(scope.ScopeHotelId, zaaerId, type.TicketTypeId),
                            TicketStatus = TicketStatusIssued,
                            UnitPrice = calc.NetAmount,
                            VatAmount = calc.VatAmount,
                            TotalAmount = calc.Total,
                            ValidFrom = validFrom,
                            ValidTo = validTo,
                            ZaaerId = zaaerId,
                            CreatedAt = now
                        });
                    }
                }

                var subtotal = tickets.Sum(t => t.UnitPrice);
                var vatAmount = tickets.Sum(t => t.VatAmount);
                var totalAmount = tickets.Sum(t => t.TotalAmount);

                var order = new ResortTicketOrder
                {
                    HotelId = scope.ScopeHotelId,
                    OrderNo = orderIdentity.DocumentNo,
                    ReservationId = reservationKey,
                    UnitId = dto.UnitId,
                    CustomerId = dto.CustomerId,
                    InvoiceId = null,
                    ReceiptId = null,
                    OrderDate = now,
                    ServiceDate = serviceDate,
                    Subtotal = subtotal,
                    VatAmount = vatAmount,
                    TotalAmount = totalAmount,
                    PaymentStatus = dto.PayNow ? "paid" : "unpaid",
                    OrderStatus = StatusActive,
                    Notes = dto.Notes,
                    CreatedBy = _currentUser.UserId,
                    CreatedAt = now,
                    ZaaerId = ToNullableInt32(orderIdentity.ZaaerId)
                };
                _context.ResortTicketOrders.Add(order);
                await _context.SaveChangesAsync(cancellationToken);

                foreach (var ticket in tickets)
                {
                    ticket.TicketOrderId = order.TicketOrderId;
                }
                _context.ResortTickets.AddRange(tickets);
                await _context.SaveChangesAsync(cancellationToken);

                PaymentReceipt? receipt = null;
                if (dto.PayNow && receiptIdentity != null)
                {
                    var isCash = IsCashPaymentMethod(paymentMethod!);
                    receipt = new PaymentReceipt
                    {
                        HotelId = scope.ScopeHotelId,
                        ReceiptNo = receiptIdentity.DocumentNo,
                        ReservationId = reservationKey,
                        UnitId = dto.UnitId,
                        InvoiceId = null,
                        OrderId = order.TicketOrderId,
                        CustomerId = dto.CustomerId,
                        ReceiptDate = now,
                        ReceiptType = "receipt",
                        VoucherCode = "receipt",
                        AmountPaid = totalAmount,
                        PaymentMethodId = paymentMethod!.PaymentMethodId,
                        PaymentMethod = isCash ? "Cash" : paymentMethod.MethodName,
                        BankId = isCash ? null : dto.BankId,
                        TransactionNo = isCash
                            ? string.Empty
                            : (dto.TransactionNo ?? string.Empty).Trim(),
                        Notes = dto.Notes ?? string.Empty,
                        Reason = "Resort ticket sale",
                        ReceiptFrom = serviceDate,
                        ReceiptTo = serviceDate,
                        ReceiptStatus = "paid",
                        CreatedBy = _currentUser.UserId,
                        CreatedAt = now,
                        ZaaerId = ToNullableInt32(receiptIdentity.ZaaerId)
                    };
                    _context.PaymentReceipts.Add(receipt);
                    await _context.SaveChangesAsync(cancellationToken);
                    order.ReceiptId = receipt.ReceiptId;
                }
                _context.ResortTicketEvents.Add(new ResortTicketEvent
                {
                    HotelId = scope.ScopeHotelId,
                    TicketOrderId = order.TicketOrderId,
                    EventType = "issued",
                    EventNote = $"Issued {tickets.Count} ticket(s).",
                    CreatedBy = _currentUser.UserId,
                    CreatedAt = now
                });

                await _context.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                foreach (var auditId in auditIds)
                {
                    await _numberingService.MarkCommittedAsync(auditId, cancellationToken);
                }

                return (await MapOrdersAsync(new[] { order }, cancellationToken))[0];
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken);
                foreach (var auditId in auditIds)
                {
                    await _numberingService.MarkVoidedAsync(auditId, ex.Message, cancellationToken);
                }

                throw;
            }
        }

        public async Task<PmsResortTicketOrderDto?> CancelOrderAsync(int id, PmsCancelResortTicketOrderDto dto, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(dto.Reason))
            {
                throw new ArgumentException("Cancellation reason is required.");
            }

            var scope = await GetCurrentResortScopeAsync(cancellationToken);
            var order = await FindOrderByTicketOrderIdAsync(scope, id, tracking: true, cancellationToken);
            if (order == null)
            {
                return null;
            }

            if (string.Equals(order.OrderStatus, StatusCancelled, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Ticket order is already cancelled.");
            }

            var tickets = await _context.ResortTickets
                .Where(t => t.HotelId == order.HotelId && t.TicketOrderId == order.TicketOrderId)
                .ToListAsync(cancellationToken);

            var hadPaidReceipt = order.ReceiptId.HasValue
                && string.Equals(order.PaymentStatus, PaymentStatusPaid, StringComparison.OrdinalIgnoreCase);
            if (hadPaidReceipt && !dto.ConfirmPaidRefund)
            {
                throw new ArgumentException("Paid order cancellation requires explicit confirmation.");
            }

            Invoice? invoice = null;
            if (order.InvoiceId.HasValue)
            {
                invoice = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.InvoiceId == order.InvoiceId.Value, cancellationToken);
            }

            PaymentReceipt? receipt = null;
            if (order.ReceiptId.HasValue)
            {
                receipt = await _context.PaymentReceipts
                    .FirstOrDefaultAsync(r => r.ReceiptId == order.ReceiptId.Value, cancellationToken);
            }

            var reason = dto.Reason.Trim();
            var now = KsaTime.Now;
            var auditIds = new List<long>();

            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                order.OrderStatus = StatusCancelled;
                order.CancelReason = reason;
                order.CancelledAt = now;
                order.CancelledBy = _currentUser.UserId;

                foreach (var ticket in tickets)
                {
                    ticket.TicketStatus = StatusCancelled;
                    ticket.CancelReason = reason;
                    ticket.CancelledAt = now;
                    ticket.CancelledBy = _currentUser.UserId;
                }

                if (invoice != null)
                {
                    var sentToZatca = invoice.IsSentZatca
                        || PmsInvoiceService.IsZatcaSubmitted(invoice.ZatcaStatus, invoice.ZatcaUuid);
                    if (sentToZatca)
                    {
                        var creditIdentity = await _numberingService.GetNextBusinessIdentityAsync(
                            NumberingDocCodes.CreditNote,
                            scope.ScopeHotelId,
                            "pms-resort-ticket-cancel",
                            $"resort-ticket-cn:{order.TicketOrderId}:{Guid.NewGuid():N}",
                            cancellationToken);
                        auditIds.Add(creditIdentity.AuditId);

                        var creditNote = new CreditNote
                        {
                            CreditNoteNo = creditIdentity.DocumentNo,
                            ZaaerId = ToNullableInt32(creditIdentity.ZaaerId),
                            HotelId = scope.ScopeHotelId,
                            InvoiceId = PmsInvoiceService.ResolveInvoiceForeignKey(invoice),
                            OrderId = order.TicketOrderId,
                            ReservationId = invoice.ReservationId,
                            CustomerId = invoice.CustomerId,
                            CreditNoteDate = now.Date,
                            CreditAmount = invoice.TotalAmount ?? order.TotalAmount,
                            Reason = reason,
                            CreditType = await ZatcaReservationLinkage.ResolveCreditNoteTypeAsync(
                                _context,
                                invoice.ReservationId,
                                invoice.HotelId,
                                cancellationToken),
                            Notes = $"Resort ticket order {order.OrderNo} cancelled.",
                            IsSentZatca = false,
                            ZatcaStatus = ZatcaApiConstants.StatusPending,
                            ZatcaUuid = Guid.NewGuid().ToString(),
                            CreatedBy = _currentUser.UserId,
                            CreatedAt = now
                        };
                        await DocumentTaxComputation.ApplyCreditNoteTaxesAsync(_context, creditNote, cancellationToken);
                        _context.CreditNotes.Add(creditNote);
                        invoice.PaymentStatus = PmsInvoiceService.PaymentStatusReversed;
                    }
                    else
                    {
                        invoice.PaymentStatus = PmsInvoiceService.PaymentStatusReversed;
                    }

                    invoice.AmountPaid = 0m;
                    invoice.AmountRemaining = 0m;
                    invoice.Notes = AppendNote(invoice.Notes, $"Ticket order cancelled: {reason}");
                }

                if (receipt != null)
                {
                    receipt.ReceiptStatus = StatusCancelled;
                    receipt.Notes = AppendNote(receipt.Notes, $"Ticket order cancelled: {reason}");
                }

                if (hadPaidReceipt && order.TotalAmount > 0m)
                {
                    order.RefundReceiptId = null;
                    order.PaymentStatus = PaymentStatusRefunded;
                }
                else
                {
                    order.PaymentStatus = "cancelled";
                }

                _context.ResortTicketEvents.Add(new ResortTicketEvent
                {
                    HotelId = order.HotelId,
                    TicketOrderId = order.TicketOrderId,
                    EventType = "cancelled",
                    EventNote = reason,
                    CreatedBy = _currentUser.UserId,
                    CreatedAt = now
                });

                await _context.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                foreach (var auditId in auditIds)
                {
                    await _numberingService.MarkCommittedAsync(auditId, cancellationToken);
                }

                var result = (await MapOrdersAsync(new[] { order }, cancellationToken))[0];
                result.Financial = await BuildOrderFinancialAsync(order, result.Tickets, cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken);
                foreach (var auditId in auditIds)
                {
                    await _numberingService.MarkVoidedAsync(auditId, ex.Message, cancellationToken);
                }

                throw;
            }
        }

        public Task<PmsResortTicketRedeemResultDto> LookupTicketByQrAsync(
            string qrCode,
            string? stationCode = null,
            CancellationToken cancellationToken = default) =>
            ResolveTicketRedeemAsync(qrCode, redeem: false, stationCode, cancellationToken);

        public Task<PmsResortTicketRedeemResultDto> RedeemTicketByQrAsync(
            string qrCode,
            string? stationCode = null,
            CancellationToken cancellationToken = default) =>
            ResolveTicketRedeemAsync(qrCode, redeem: true, stationCode, cancellationToken);

        private async Task<PmsResortTicketRedeemResultDto> ResolveTicketRedeemAsync(
            string qrCode,
            bool redeem,
            string? stationCode,
            CancellationToken cancellationToken)
        {
            var normalized = qrCode?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new ArgumentException("QR code is required.");
            }

            var scope = await GetCurrentResortScopeAsync(cancellationToken);
            var stationType = await ResolveStationTypeAsync(scope, stationCode, cancellationToken);
            var (ticket, securityBlock) = await FindTicketByQrAsync(scope, normalized, cancellationToken);
            if (securityBlock != null)
            {
                return new PmsResortTicketRedeemResultDto
                {
                    Success = false,
                    BlockReason = securityBlock
                };
            }

            if (ticket == null)
            {
                return new PmsResortTicketRedeemResultDto
                {
                    Success = false,
                    BlockReason = "not_found"
                };
            }

            var order = await _context.ResortTicketOrders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.TicketOrderId == ticket.TicketOrderId, cancellationToken);
            if (order == null)
            {
                return new PmsResortTicketRedeemResultDto
                {
                    Success = false,
                    BlockReason = "not_found"
                };
            }

            var types = await LoadTicketTypesForTicketsAsync(new[] { ticket }, cancellationToken);
            types.TryGetValue(ticket.TicketTypeId, out var ticketType);
            var now = KsaTime.Now;
            var stationBlock = EvaluateStationMatch(ticketType, stationCode, stationType);
            if (stationBlock != null)
            {
                var ticketDtoBlocked = MapTicket(ticket, types);
                return BuildRedeemResult(
                    success: false,
                    stationBlock,
                    ticketDtoBlocked,
                    order,
                    ticket,
                    ticketType,
                    now,
                    isReentry: false);
            }

            var ticketDto = MapTicket(ticket, types);
            var isReentry = string.Equals(ticket.TicketStatus, TicketStatusUsed, StringComparison.OrdinalIgnoreCase);
            var blockReason = EvaluateTicketRedeemBlock(ticket, ticketType, order, now, isReentry);
            if (blockReason != null)
            {
                return BuildRedeemResult(
                    success: false,
                    blockReason,
                    ticketDto,
                    order,
                    ticket,
                    ticketType,
                    now,
                    isReentry: false);
            }

            if (!redeem)
            {
                return BuildRedeemResult(
                    success: true,
                    blockReason: null,
                    ticketDto,
                    order,
                    ticket,
                    ticketType,
                    now,
                    isReentry);
            }

            var tracked = await _context.ResortTickets
                .FirstOrDefaultAsync(
                    t => t.TicketId == ticket.TicketId && (t.HotelId == scope.ScopeHotelId || t.HotelId == scope.LocalHotelId),
                    cancellationToken);
            if (tracked == null)
            {
                return new PmsResortTicketRedeemResultDto
                {
                    Success = false,
                    BlockReason = "not_found"
                };
            }

            var trackedReentry = string.Equals(tracked.TicketStatus, TicketStatusUsed, StringComparison.OrdinalIgnoreCase);
            var redeemBlock = EvaluateTicketRedeemBlock(tracked, ticketType, order, now, trackedReentry);
            if (redeemBlock != null)
            {
                return BuildRedeemResult(
                    success: false,
                    redeemBlock,
                    MapTicket(tracked, types),
                    order,
                    tracked,
                    ticketType,
                    now,
                    isReentry: false);
            }

            var config = await GetOrCreateConfigEntityAsync(scope, cancellationToken);

            if (trackedReentry)
            {
                _context.ResortTicketEvents.Add(new ResortTicketEvent
                {
                    HotelId = tracked.HotelId,
                    TicketId = tracked.TicketId,
                    TicketOrderId = tracked.TicketOrderId,
                    EventType = "reentry",
                    EventNote = $"Ticket {tracked.TicketNo} re-entry within validity window.",
                    CreatedBy = _currentUser.UserId,
                    CreatedAt = now
                });
            }
            else if (ticketType != null
                     && ResortTicketValidityModes.IsFromFirstScan(ticketType.ValidityMode)
                     && !tracked.SessionStartedAt.HasValue)
            {
                tracked.SessionStartedAt = now;
                tracked.ValidFrom = now;
                tracked.ValidTo = ResortTicketBusinessHours.ComputeSessionValidTo(
                    order.ServiceDate,
                    ticketType.TicketCategory,
                    config.IssueStartTime,
                    config.TicketValidityEndTime,
                    config.GamesValidityEndTime,
                    ResolveValidForMinutes(ticketType),
                    now);
                tracked.TicketStatus = TicketStatusUsed;
                tracked.UsedAt = now;
                _context.ResortTicketEvents.Add(new ResortTicketEvent
                {
                    HotelId = tracked.HotelId,
                    TicketId = tracked.TicketId,
                    TicketOrderId = tracked.TicketOrderId,
                    EventType = "session_started",
                    EventNote = $"Ticket {tracked.TicketNo} play session started ({ResolveValidForMinutes(ticketType)} min).",
                    CreatedBy = _currentUser.UserId,
                    CreatedAt = now
                });
            }
            else
            {
                tracked.TicketStatus = TicketStatusUsed;
                tracked.UsedAt = now;
                _context.ResortTicketEvents.Add(new ResortTicketEvent
                {
                    HotelId = tracked.HotelId,
                    TicketId = tracked.TicketId,
                    TicketOrderId = tracked.TicketOrderId,
                    EventType = "used",
                    EventNote = $"Ticket {tracked.TicketNo} redeemed at gate.",
                    CreatedBy = _currentUser.UserId,
                    CreatedAt = now
                });
            }

            await _context.SaveChangesAsync(cancellationToken);

            ticketDto = MapTicket(tracked, types);
            return BuildRedeemResult(
                success: true,
                blockReason: null,
                ticketDto,
                order,
                tracked,
                ticketType,
                now,
                isReentry: trackedReentry,
                redeemedAt: trackedReentry ? tracked.UsedAt : now);
        }

        private async Task<(ResortTicket? Ticket, string? SecurityBlock)> FindTicketByQrAsync(
            ResortScope scope,
            string qrCode,
            CancellationToken cancellationToken)
        {
            var normalized = qrCode.Trim();
            if (!_qrSecurity.IsKnownFormat(normalized))
            {
                return (null, "forged");
            }

            var hotelIds = new[] { scope.ScopeHotelId, scope.LocalHotelId }.Distinct().ToList();

            var exact = await _context.ResortTickets
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    t => hotelIds.Contains(t.HotelId) && t.QrCode == normalized,
                    cancellationToken);
            if (exact != null)
            {
                if (_qrSecurity.IsSignedFormat(normalized)
                    && !_qrSecurity.VerifySignedForTicket(
                        exact.HotelId,
                        exact.ZaaerId ?? 0,
                        exact.TicketTypeId,
                        normalized))
                {
                    return (null, "forged");
                }

                return (exact, null);
            }

            if (_qrSecurity.TryParseSigned(normalized, out var signedHotelId, out var signedZaaerId, out _))
            {
                if (!hotelIds.Contains(signedHotelId))
                {
                    return (null, "forged");
                }

                var signedTicket = await _context.ResortTickets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        t => hotelIds.Contains(t.HotelId) && t.ZaaerId == signedZaaerId,
                        cancellationToken);
                if (signedTicket == null)
                {
                    return (null, "not_found");
                }

                if (!_qrSecurity.VerifySignedForTicket(
                        signedTicket.HotelId,
                        signedTicket.ZaaerId ?? 0,
                        signedTicket.TicketTypeId,
                        normalized))
                {
                    return (null, "forged");
                }

                return (signedTicket, null);
            }

            if (_qrSecurity.TryParseLegacy(normalized, out var legacyHotelId, out var legacyZaaerId))
            {
                if (!hotelIds.Contains(legacyHotelId))
                {
                    return (null, "forged");
                }

                var legacyTicket = await _context.ResortTickets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        t => hotelIds.Contains(t.HotelId)
                            && (t.QrCode == normalized
                                || (t.ZaaerId == legacyZaaerId && t.QrCode.StartsWith("RSRT-", StringComparison.OrdinalIgnoreCase))),
                        cancellationToken);
                return legacyTicket == null ? (null, "not_found") : (legacyTicket, null);
            }

            return (null, "forged");
        }

        private static string ResolveValidityStatus(ResortTicket ticket, ResortTicketType? type, DateTime now)
        {
            if (type != null
                && ResortTicketValidityModes.IsFromFirstScan(type.ValidityMode)
                && !ticket.SessionStartedAt.HasValue)
            {
                if (now > ticket.ValidTo)
                {
                    return "expired";
                }

                return "pending_activation";
            }

            if (now < ticket.ValidFrom)
            {
                return "not_yet_valid";
            }

            if (now > ticket.ValidTo)
            {
                return "expired";
            }

            return "valid";
        }

        private static int? ResolveRemainingMinutes(ResortTicket ticket, DateTime now)
        {
            if (!ticket.SessionStartedAt.HasValue || now > ticket.ValidTo)
            {
                return null;
            }

            var remaining = (int)Math.Ceiling((ticket.ValidTo - now).TotalMinutes);
            return remaining > 0 ? remaining : 0;
        }

        private static PmsResortTicketRedeemResultDto BuildRedeemResult(
            bool success,
            string? blockReason,
            PmsResortTicketDto ticketDto,
            ResortTicketOrder order,
            ResortTicket ticket,
            ResortTicketType? type,
            DateTime now,
            bool isReentry,
            DateTime? redeemedAt = null)
        {
            return new PmsResortTicketRedeemResultDto
            {
                Success = success,
                BlockReason = blockReason,
                Ticket = ticketDto,
                OrderNo = order.OrderNo,
                OrderStatus = order.OrderStatus,
                RedeemedAt = redeemedAt ?? ticket.UsedAt,
                IsReentry = success && isReentry,
                ValidityStatus = ResolveValidityStatus(ticket, type, now),
                ValidFrom = ticket.ValidFrom,
                ValidTo = ticket.ValidTo,
                RemainingMinutes = ResolveRemainingMinutes(ticket, now)
            };
        }

        private static string? EvaluateTicketRedeemBlock(
            ResortTicket ticket,
            ResortTicketType? type,
            ResortTicketOrder order,
            DateTime now,
            bool isReentryWithinValidity)
        {
            if (string.Equals(order.OrderStatus, StatusCancelled, StringComparison.OrdinalIgnoreCase))
            {
                return "order_cancelled";
            }

            if (string.Equals(ticket.TicketStatus, StatusCancelled, StringComparison.OrdinalIgnoreCase))
            {
                return "ticket_cancelled";
            }

            if (type != null
                && ResortTicketValidityModes.IsFromFirstScan(type.ValidityMode)
                && !ticket.SessionStartedAt.HasValue)
            {
                if (now > ticket.ValidTo)
                {
                    return isReentryWithinValidity ? "expired_reentry" : "expired";
                }

                return null;
            }

            if (now < ticket.ValidFrom)
            {
                return "not_yet_valid";
            }

            if (now > ticket.ValidTo)
            {
                return isReentryWithinValidity ? "expired_reentry" : "expired";
            }

            return null;
        }

        private static int ResolveValidForMinutes(ResortTicketType type) =>
            type.ValidForMinutes > 0 ? type.ValidForMinutes : type.ValidForHours * 60;

        private static int ResolveValidForMinutesFromDto(PmsUpsertResortTicketTypeDto dto) =>
            dto.ValidForMinutes > 0 ? dto.ValidForMinutes : dto.ValidForHours * 60;

        private static string ResolveValidityModeFromDto(PmsUpsertResortTicketTypeDto dto)
        {
            if (!string.IsNullOrWhiteSpace(dto.ValidityMode))
            {
                return ResortTicketValidityModes.Normalize(dto.ValidityMode);
            }

            var category = ResortTicketCategories.Normalize(dto.TicketCategory);
            if (string.Equals(category, ResortTicketCategories.Games, StringComparison.OrdinalIgnoreCase)
                && !dto.IsGeneric)
            {
                return ResortTicketValidityModes.FromFirstScan;
            }

            return ResortTicketValidityModes.BusinessDay;
        }

        private async Task<IReadOnlyDictionary<int, ResortTicketType>> LoadTicketTypesForTicketsAsync(
            IReadOnlyList<ResortTicket> tickets,
            CancellationToken cancellationToken)
        {
            var typeIds = tickets.Select(t => t.TicketTypeId).Distinct().ToList();
            if (typeIds.Count == 0)
            {
                return new Dictionary<int, ResortTicketType>();
            }

            var types = await _context.ResortTicketTypes.AsNoTracking()
                .Where(t => typeIds.Contains(t.TicketTypeId))
                .ToListAsync(cancellationToken);
            return types.ToDictionary(t => t.TicketTypeId);
        }

        public async Task<PmsResortTicketBusinessConfigDto> GetBusinessConfigAsync(CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentResortScopeAsync(cancellationToken);
            var config = await GetOrCreateConfigEntityAsync(scope, cancellationToken);
            return MapBusinessConfigDto(config, KsaTime.Now);
        }

        public async Task<PmsResortTicketBusinessConfigDto> UpdateBusinessConfigAsync(
            PmsUpsertResortTicketBusinessConfigDto dto,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentResortScopeAsync(cancellationToken);
            var config = await GetOrCreateConfigEntityAsync(scope, cancellationToken, tracking: true);
            config.IssueStartTime = ParseTimeOfDay(dto.IssueStartTime);
            config.TicketValidityEndTime = ParseTimeOfDay(dto.TicketValidityEndTime);
            config.GamesValidityEndTime = string.IsNullOrWhiteSpace(dto.GamesValidityEndTime)
                ? null
                : ParseTimeOfDay(dto.GamesValidityEndTime);
            config.DailyCloseTime = ParseTimeOfDay(dto.DailyCloseTime);
            config.UpdatedAt = KsaTime.Now;
            await _context.SaveChangesAsync(cancellationToken);
            return MapBusinessConfigDto(config, KsaTime.Now);
        }

        public async Task<IReadOnlyList<PmsResortTicketPendingInvoiceOrderDto>> ListPendingInvoiceOrdersAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentResortScopeAsync(cancellationToken);
            var query = _context.ResortTicketOrders.AsNoTracking()
                .Where(o => (o.HotelId == scope.ScopeHotelId || o.HotelId == scope.LocalHotelId)
                    && o.InvoiceId == null
                    && o.ReceiptId != null
                    && o.OrderStatus != StatusCancelled
                    && o.PaymentStatus == PaymentStatusPaid);

            if (fromDate.HasValue)
            {
                var from = NormalizeQueryDate(fromDate)!.Value;
                query = query.Where(o => o.ServiceDate >= from);
            }

            if (toDate.HasValue)
            {
                var to = NormalizeQueryDate(toDate)!.Value;
                query = query.Where(o => o.ServiceDate <= to);
            }

            var orders = await query
                .OrderByDescending(o => o.ServiceDate)
                .ThenByDescending(o => o.TicketOrderId)
                .ToListAsync(cancellationToken);

            if (orders.Count == 0)
            {
                return Array.Empty<PmsResortTicketPendingInvoiceOrderDto>();
            }

            var orderIds = orders.Select(o => o.TicketOrderId).ToList();
            var receiptIds = orders.Where(o => o.ReceiptId.HasValue).Select(o => o.ReceiptId!.Value).Distinct().ToList();
            var receipts = await _context.PaymentReceipts.AsNoTracking()
                .Where(r => receiptIds.Contains(r.ReceiptId))
                .ToDictionaryAsync(r => r.ReceiptId, cancellationToken);
            var ticketCounts = await _context.ResortTickets.AsNoTracking()
                .Where(t => orderIds.Contains(t.TicketOrderId))
                .GroupBy(t => t.TicketOrderId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

            return orders.Select(o =>
            {
                receipts.TryGetValue(o.ReceiptId ?? 0, out var receipt);
                ticketCounts.TryGetValue(o.TicketOrderId, out var count);
                return new PmsResortTicketPendingInvoiceOrderDto
                {
                    TicketOrderId = o.TicketOrderId,
                    OrderNo = o.OrderNo,
                    ServiceDate = o.ServiceDate,
                    TotalAmount = o.TotalAmount,
                    ReceiptNo = receipt?.ReceiptNo,
                    ReceiptId = o.ReceiptId,
                    TicketCount = count,
                    PaymentStatus = o.PaymentStatus
                };
            }).ToList();
        }

        public async Task<IReadOnlyList<PmsResortTicketInvoiceListItemDto>> ListTicketInvoicesAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentResortScopeAsync(cancellationToken);
            var query = _context.Invoices.AsNoTracking()
                .Where(i => (i.HotelId == scope.ScopeHotelId || i.HotelId == scope.LocalHotelId)
                    && i.InvoiceType == InvoiceTypeTicketSale);

            if (fromDate.HasValue)
            {
                var from = NormalizeQueryDate(fromDate)!.Value;
                query = query.Where(i => i.InvoiceDate >= from);
            }

            if (toDate.HasValue)
            {
                var to = NormalizeQueryDate(toDate)!.Value;
                query = query.Where(i => i.InvoiceDate <= to);
            }

            var invoices = await query
                .OrderByDescending(i => i.InvoiceDate)
                .ThenByDescending(i => i.InvoiceId)
                .ToListAsync(cancellationToken);

            return await MapTicketInvoiceListAsync(invoices, cancellationToken);
        }

        public async Task<IReadOnlyList<PmsResortTicketReceiptListItemDto>> ListTicketReceiptsAsync(
            string? receiptKind = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentResortScopeAsync(cancellationToken);
            var orderQuery = _context.ResortTicketOrders.AsNoTracking()
                .Where(o => o.HotelId == scope.ScopeHotelId || o.HotelId == scope.LocalHotelId);

            var query =
                from receipt in _context.PaymentReceipts.AsNoTracking()
                join order in orderQuery on receipt.OrderId equals order.TicketOrderId
                where (receipt.HotelId == scope.ScopeHotelId || receipt.HotelId == scope.LocalHotelId)
                    && receipt.OrderId.HasValue
                select new { receipt, order };

            var kind = (receiptKind ?? string.Empty).Trim().ToLowerInvariant();
            if (kind == "collection")
            {
                query = query.Where(x =>
                    x.receipt.ReceiptType == "receipt"
                    && x.receipt.AmountPaid >= 0m);
            }
            else if (kind == "disbursement")
            {
                query = query.Where(x =>
                    x.receipt.ReceiptType == "refund"
                    || x.receipt.AmountPaid < 0m);
            }

            if (fromDate.HasValue)
            {
                var from = NormalizeQueryDate(fromDate)!.Value;
                query = query.Where(x => x.receipt.ReceiptDate >= from);
            }

            if (toDate.HasValue)
            {
                var to = NormalizeQueryDate(toDate)!.Value;
                var exclusiveTo = to.AddDays(1);
                query = query.Where(x => x.receipt.ReceiptDate < exclusiveTo);
            }

            var rows = await query
                .OrderByDescending(x => x.receipt.ReceiptDate)
                .ThenByDescending(x => x.receipt.ReceiptId)
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                return Array.Empty<PmsResortTicketReceiptListItemDto>();
            }

            var invoiceIds = rows
                .Where(x => x.order.InvoiceId.HasValue)
                .Select(x => x.order.InvoiceId!.Value)
                .Distinct()
                .ToList();
            var invoices = invoiceIds.Count == 0
                ? new Dictionary<int, Invoice>()
                : await _context.Invoices.AsNoTracking()
                    .Where(i => invoiceIds.Contains(i.InvoiceId))
                    .ToDictionaryAsync(i => i.InvoiceId, cancellationToken);

            return rows.Select(x =>
            {
                Invoice? invoice = null;
                if (x.order.InvoiceId.HasValue)
                {
                    invoices.TryGetValue(x.order.InvoiceId.Value, out invoice);
                }

                return new PmsResortTicketReceiptListItemDto
                {
                    ReceiptId = x.receipt.ReceiptId,
                    ReceiptNo = x.receipt.ReceiptNo,
                    ReceiptDate = x.receipt.ReceiptDate,
                    AmountPaid = x.receipt.AmountPaid,
                    ReceiptType = x.receipt.ReceiptType,
                    PaymentMethod = x.receipt.PaymentMethod,
                    ReceiptStatus = x.receipt.ReceiptStatus ?? string.Empty,
                    TicketOrderId = x.order.TicketOrderId,
                    OrderNo = x.order.OrderNo,
                    ServiceDate = x.order.ServiceDate,
                    InvoiceId = x.order.InvoiceId,
                    InvoiceNo = invoice?.InvoiceNo,
                    HasInvoice = x.order.InvoiceId.HasValue,
                    OrderPaymentStatus = x.order.PaymentStatus
                };
            }).ToList();
        }

        public async Task<PmsResortTicketFinanceReconciliationDto> GetFinanceReconciliationAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken cancellationToken = default)
        {
            var pending = await ListPendingInvoiceOrdersAsync(fromDate, toDate, cancellationToken);
            var collections = await ListTicketReceiptsAsync("collection", fromDate, toDate, cancellationToken);
            var disbursements = await ListTicketReceiptsAsync("disbursement", fromDate, toDate, cancellationToken);
            var invoices = await ListTicketInvoicesAsync(fromDate, toDate, cancellationToken);

            var pendingTotal = pending.Sum(o => o.TotalAmount);
            var pendingOrderIds = pending.Select(o => o.TicketOrderId).ToHashSet();
            var pendingCollections = collections.Where(r => pendingOrderIds.Contains(r.TicketOrderId)).ToList();
            var pendingCollectionTotal = pendingCollections.Sum(r => r.AmountPaid);
            var collectionTotal = collections.Sum(r => r.AmountPaid);
            var disbursementTotal = disbursements.Sum(r => Math.Abs(r.AmountPaid));
            var invoicedTotal = invoices.Sum(i => i.TotalAmount);
            var variance = pendingTotal - pendingCollectionTotal;

            return new PmsResortTicketFinanceReconciliationDto
            {
                PendingInvoiceOrderCount = pending.Count,
                PendingInvoiceOrderTotal = pendingTotal,
                CollectionReceiptsTotal = collectionTotal,
                CollectionReceiptCount = collections.Count,
                DisbursementReceiptsTotal = disbursementTotal,
                DisbursementReceiptCount = disbursements.Count,
                NetReceiptsTotal = collectionTotal - disbursementTotal,
                InvoicedTotal = invoicedTotal,
                InvoicedCount = invoices.Count,
                PendingVsCollectionVariance = variance,
                IsBalanced = Math.Abs(variance) < 0.01m
            };
        }

        public async Task<IReadOnlyList<PmsResortTicketInvoiceListItemDto>> CreateInvoicesForOrdersAsync(
            PmsCreateResortTicketInvoicesDto dto,
            CancellationToken cancellationToken = default)
        {
            if (dto.TicketOrderIds.Count == 0)
            {
                throw new ArgumentException("At least one ticket order is required.");
            }

            var scope = await GetCurrentResortScopeAsync(cancellationToken);
            var orderIds = dto.TicketOrderIds.Distinct().ToList();
            var orders = await _context.ResortTicketOrders
                .Where(o => (o.HotelId == scope.ScopeHotelId || o.HotelId == scope.LocalHotelId)
                    && orderIds.Contains(o.TicketOrderId))
                .ToListAsync(cancellationToken);

            if (orders.Count != orderIds.Count)
            {
                throw new ArgumentException("One or more ticket orders were not found.");
            }

            var taxConfig = await ResolveTicketTaxConfigAsync(scope, cancellationToken);
            var now = KsaTime.Now;
            var createdInvoiceIds = new List<int>();

            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                foreach (var order in orders)
                {
                    if (order.InvoiceId.HasValue)
                    {
                        throw new InvalidOperationException($"Order {order.OrderNo} already has an invoice.");
                    }

                    if (!order.ReceiptId.HasValue)
                    {
                        throw new InvalidOperationException($"Order {order.OrderNo} has no payment receipt.");
                    }

                    var receipt = await _context.PaymentReceipts
                        .FirstOrDefaultAsync(r => r.ReceiptId == order.ReceiptId.Value, cancellationToken)
                        ?? throw new InvalidOperationException($"Receipt for order {order.OrderNo} was not found.");

                    var invoiceIdentity = await _numberingService.GetNextBusinessIdentityAsync(
                        NumberingDocCodes.Invoice,
                        scope.ScopeHotelId,
                        "pms-resort-ticket-invoice",
                        $"resort-ticket-invoice:{order.OrderNo}:{Guid.NewGuid():N}",
                        cancellationToken);

                    var invoice = new Invoice
                    {
                        HotelId = scope.ScopeHotelId,
                        InvoiceNo = invoiceIdentity.DocumentNo,
                        OrderId = order.TicketOrderId,
                        ReservationId = order.ReservationId,
                        UnitId = order.UnitId,
                        CustomerId = order.CustomerId,
                        InvoiceDate = now,
                        PeriodFrom = order.ServiceDate,
                        PeriodTo = order.ServiceDate,
                        InvoiceType = InvoiceTypeTicketSale,
                        Subtotal = order.Subtotal,
                        VatRate = taxConfig.VatRate,
                        VatAmount = order.VatAmount,
                        TotalAmount = order.TotalAmount,
                        PaymentStatus = PaymentStatusPaid,
                        AmountPaid = order.TotalAmount,
                        AmountRemaining = 0m,
                        IsSentZatca = false,
                        ZatcaStatus = ZatcaApiConstants.StatusPending,
                        ZatcaUuid = Guid.NewGuid().ToString(),
                        CreatedBy = _currentUser.UserId,
                        CreatedAt = now,
                        Notes = order.Notes,
                        ZaaerId = ToNullableInt32(invoiceIdentity.ZaaerId),
                        RevenueCategory = "tickets"
                    };
                    _context.Invoices.Add(invoice);
                    await _context.SaveChangesAsync(cancellationToken);

                    receipt.InvoiceId = invoice.ZaaerId ?? invoice.InvoiceId;
                    order.InvoiceId = invoice.InvoiceId;

                    _context.InvoiceReceiptMappings.Add(new InvoiceReceiptMapping
                    {
                        InvoiceId = invoice.InvoiceId,
                        ReceiptId = receipt.ReceiptId,
                        AllocatedAmount = order.TotalAmount,
                        MappingDate = now,
                        CreatedAt = now,
                        CreatedBy = _currentUser.UserId
                    });

                    await _numberingService.MarkCommittedAsync(invoiceIdentity.AuditId, cancellationToken);
                    createdInvoiceIds.Add(invoice.InvoiceId);
                }

                await _context.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }

            var created = await _context.Invoices.AsNoTracking()
                .Where(i => createdInvoiceIds.Contains(i.InvoiceId))
                .ToListAsync(cancellationToken);
            return await MapTicketInvoiceListAsync(created, cancellationToken);
        }

        public async Task<ReportRenderResult?> PrintOrderAsync(int id, string paper = "thermal", CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentResortScopeAsync(cancellationToken);
            var order = await FindOrderByTicketOrderIdAsync(scope, id, tracking: false, cancellationToken);
            if (order == null)
            {
                return null;
            }

            var dto = (await MapOrdersAsync(new[] { order }, cancellationToken))[0];
            await MarkPrintedAsync(order.TicketOrderId, cancellationToken);
            var hotel = await LoadHotelAsync(scope, cancellationToken);
            var logoBytes = await LoadLogoBytesAsync(hotel, scope, cancellationToken);
            var report = BuildTicketReport(dto, hotel, paper, logoBytes);
            return await _renderService.ExportToPdfAsync(report, $"resort-ticket-order-{order.OrderNo}", cancellationToken);
        }

        public async Task<ReportRenderResult?> PrintTicketAsync(int id, string paper = "thermal", CancellationToken cancellationToken = default)
        {
            var scope = await GetCurrentResortScopeAsync(cancellationToken);
            var ticket = await _context.ResortTickets.AsNoTracking()
                .FirstOrDefaultAsync(t => (t.HotelId == scope.ScopeHotelId || t.HotelId == scope.LocalHotelId) && t.TicketId == id, cancellationToken);
            if (ticket == null)
            {
                return null;
            }

            var order = await _context.ResortTicketOrders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.TicketOrderId == ticket.TicketOrderId, cancellationToken);
            if (order == null)
            {
                return null;
            }

            await MarkPrintedAsync(order.TicketOrderId, cancellationToken, ticket.TicketId);
            var dto = (await MapOrdersAsync(new[] { order }, cancellationToken))[0];
            dto.Tickets = dto.Tickets.Where(t => t.TicketId == ticket.TicketId).ToList();
            var hotel = await LoadHotelAsync(scope, cancellationToken);
            var logoBytes = await LoadLogoBytesAsync(hotel, scope, cancellationToken);
            var report = BuildTicketReport(dto, hotel, paper, logoBytes);
            return await _renderService.ExportToPdfAsync(report, $"resort-ticket-{ticket.TicketNo}", cancellationToken);
        }

        private async Task MarkPrintedAsync(int orderId, CancellationToken cancellationToken, int? ticketId = null)
        {
            var now = KsaTime.Now;
            var query = _context.ResortTickets.Where(t => t.TicketOrderId == orderId);
            if (ticketId.HasValue)
            {
                query = query.Where(t => t.TicketId == ticketId.Value);
            }

            var tickets = await query.ToListAsync(cancellationToken);
            foreach (var ticket in tickets.Where(t => t.TicketStatus == TicketStatusIssued || t.TicketStatus == TicketStatusPrinted))
            {
                ticket.TicketStatus = TicketStatusPrinted;
                ticket.PrintedAt = now;
                _context.ResortTicketEvents.Add(new ResortTicketEvent
                {
                    HotelId = ticket.HotelId,
                    TicketId = ticket.TicketId,
                    TicketOrderId = ticket.TicketOrderId,
                    EventType = "printed",
                    CreatedBy = _currentUser.UserId,
                    CreatedAt = now
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<PmsResortTicketOrderFinancialDto> BuildOrderFinancialAsync(
            ResortTicketOrder order,
            IReadOnlyList<PmsResortTicketDto> tickets,
            CancellationToken cancellationToken)
        {
            Invoice? invoice = null;
            if (order.InvoiceId.HasValue)
            {
                invoice = await _context.Invoices.AsNoTracking()
                    .FirstOrDefaultAsync(i => i.InvoiceId == order.InvoiceId.Value, cancellationToken);
            }

            PaymentReceipt? receipt = null;
            if (order.ReceiptId.HasValue)
            {
                receipt = await _context.PaymentReceipts.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.ReceiptId == order.ReceiptId.Value, cancellationToken);
            }

            PaymentReceipt? refundReceipt = null;
            if (order.RefundReceiptId.HasValue)
            {
                refundReceipt = await _context.PaymentReceipts.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.ReceiptId == order.RefundReceiptId.Value, cancellationToken);
            }

            CreditNote? creditNote = null;
            if (invoice != null)
            {
                var invoiceFk = PmsInvoiceService.ResolveInvoiceForeignKey(invoice);
                creditNote = await _context.CreditNotes.AsNoTracking()
                    .Where(c => c.InvoiceId == invoiceFk)
                    .OrderByDescending(c => c.CreditNoteId)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            var usedCount = tickets.Count(t => string.Equals(t.TicketStatus, TicketStatusUsed, StringComparison.OrdinalIgnoreCase));
            var cancelledCount = tickets.Count(t => string.Equals(t.TicketStatus, StatusCancelled, StringComparison.OrdinalIgnoreCase));
            var isCancelled = string.Equals(order.OrderStatus, StatusCancelled, StringComparison.OrdinalIgnoreCase);
            var sentToZatca = invoice != null
                && (invoice.IsSentZatca || PmsInvoiceService.IsZatcaSubmitted(invoice.ZatcaStatus, invoice.ZatcaUuid));
            var hadPaidReceipt = receipt != null
                && string.Equals(receipt.ReceiptStatus, ReceiptStatusPaid, StringComparison.OrdinalIgnoreCase)
                && string.Equals(order.PaymentStatus, PaymentStatusPaid, StringComparison.OrdinalIgnoreCase);

            string? blockReason = null;
            if (isCancelled)
            {
                blockReason = "already_cancelled";
            }

            return new PmsResortTicketOrderFinancialDto
            {
                InvoiceNo = invoice?.InvoiceNo,
                InvoiceZaaerId = invoice?.ZaaerId ?? invoice?.InvoiceId,
                InvoicePaymentStatus = invoice?.PaymentStatus,
                InvoiceSentToZatca = sentToZatca,
                InvoiceZatcaStatus = invoice?.ZatcaStatus,
                ReceiptNo = receipt?.ReceiptNo,
                ReceiptStatus = receipt?.ReceiptStatus,
                RefundReceiptNo = refundReceipt?.ReceiptNo,
                RefundReceiptId = refundReceipt?.ReceiptId ?? order.RefundReceiptId,
                CreditNoteNo = creditNote?.CreditNoteNo,
                CreditNoteId = creditNote?.CreditNoteId,
                CanCancel = blockReason == null,
                CancelBlockReason = blockReason,
                IssuedTicketCount = tickets.Count,
                UsedTicketCount = usedCount,
                CancelledTicketCount = cancelledCount,
                WillCreateRefundDisbursement = false,
                WillCreateCreditNote = invoice != null && sentToZatca && !isCancelled,
                WillReverseInvoiceOnly = invoice != null && !sentToZatca && !isCancelled
            };
        }

        private async Task<IReadOnlyList<PmsResortTicketOrderDto>> MapOrdersAsync(
            IReadOnlyList<ResortTicketOrder> orders,
            CancellationToken cancellationToken)
        {
            if (orders.Count == 0)
            {
                return Array.Empty<PmsResortTicketOrderDto>();
            }

            var orderIds = orders.Select(o => o.TicketOrderId).ToList();
            var hotelIds = orders.Select(o => o.HotelId).Distinct().ToList();
            var tickets = await _context.ResortTickets.AsNoTracking()
                .Where(t => hotelIds.Contains(t.HotelId) && orderIds.Contains(t.TicketOrderId))
                .ToListAsync(cancellationToken);
            var typeIds = tickets.Select(t => t.TicketTypeId).Distinct().ToList();
            var types = await _context.ResortTicketTypes.AsNoTracking()
                .Where(t => typeIds.Contains(t.TicketTypeId))
                .ToDictionaryAsync(t => t.TicketTypeId, cancellationToken);

            return orders.Select(order => new PmsResortTicketOrderDto
            {
                TicketOrderId = order.TicketOrderId,
                ZaaerId = order.ZaaerId,
                OrderNo = order.OrderNo,
                ReservationId = order.ReservationId,
                UnitId = order.UnitId,
                CustomerId = order.CustomerId,
                InvoiceId = order.InvoiceId,
                ReceiptId = order.ReceiptId,
                RefundReceiptId = order.RefundReceiptId,
                OrderDate = order.OrderDate,
                ServiceDate = order.ServiceDate,
                Subtotal = order.Subtotal,
                VatAmount = order.VatAmount,
                TotalAmount = order.TotalAmount,
                PaymentStatus = order.PaymentStatus,
                OrderStatus = order.OrderStatus,
                Notes = order.Notes,
                CancelReason = order.CancelReason,
                Tickets = tickets
                    .Where(t => t.TicketOrderId == order.TicketOrderId)
                    .OrderBy(t => t.TicketNo)
                    .Select(t => MapTicket(t, types))
                    .ToList()
            }).ToList();
        }

        private XtraReport BuildTicketReport(PmsResortTicketOrderDto order, HotelSettings hotel, string paper, byte[]? logoBytes)
        {
            var report = new XtraReport
            {
                DisplayName = "Resort Tickets",
                ReportUnit = ReportUnit.HundredthsOfAnInch,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = RightToLeftLayout.Yes,
                PaperKind = DXPaperKind.Custom,
                PageWidth = string.Equals(paper, "a4", StringComparison.OrdinalIgnoreCase) ? 827 : 315,
                PageHeight = string.Equals(paper, "a4", StringComparison.OrdinalIgnoreCase) ? 1169 : Math.Max(460, 330 * Math.Max(1, order.Tickets.Count)),
                Margins = new System.Drawing.Printing.Margins(12, 12, 12, 12)
            };

            var detail = new DetailBand { HeightF = 320 };
            report.Bands.Add(detail);
            var top = 0f;
            foreach (var ticket in order.Tickets)
            {
                AddTicketPanel(detail, ticket, order, hotel, top, report.PageWidth - 24, logoBytes);
                top += 320f;
            }

            detail.HeightF = Math.Max(320, top);
            return report;
        }

        private static void AddTicketPanel(
            DetailBand detail,
            PmsResortTicketDto ticket,
            PmsResortTicketOrderDto order,
            HotelSettings hotel,
            float top,
            float width,
            byte[]? logoBytes)
        {
            var panel = new XRPanel
            {
                BoundsF = new RectangleF(0, top, width, 300),
                Borders = BorderSide.All,
                BorderColor = Color.FromArgb(63, 111, 159),
                BackColor = Color.White
            };
            detail.Controls.Add(panel);

            if (TryCreateImage(logoBytes, out var logo))
            {
                panel.Controls.Add(new XRPictureBox
                {
                    Image = logo,
                    Sizing = ImageSizeMode.ZoomImage,
                    BoundsF = new RectangleF((width - 58) / 2, 8, 58, 32)
                });
            }

            var titleTop = logoBytes is { Length: > 0 } ? 42f : 10f;
            panel.Controls.Add(new XRLabel
            {
                Text = hotel.HotelName ?? "Resort",
                BoundsF = new RectangleF(10, titleTop, width - 20, 24),
                Font = new Font("Arial", 14, FontStyle.Bold),
                TextAlignment = TextAlignment.MiddleCenter
            });
            panel.Controls.Add(new XRLabel
            {
                Text = "تذكرة دخول / Resort Ticket",
                BoundsF = new RectangleF(10, titleTop + 28, width - 20, 22),
                Font = new Font("Arial", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(63, 111, 159),
                TextAlignment = TextAlignment.MiddleCenter
            });
            var qrBarcode = new XRBarCode
            {
                Text = ticket.QrCode,
                Symbology = new QRCodeGenerator(),
                BoundsF = new RectangleF((width - 130) / 2, 86, 130, 130),
                AutoModule = true,
                ShowText = false
            };
            ConfigureResortTicketQrSymbology(qrBarcode);
            panel.Controls.Add(qrBarcode);
            panel.Controls.Add(new XRLabel
            {
                Text = ticket.TicketNo,
                BoundsF = new RectangleF(10, 222, width - 20, 24),
                Font = new Font("Arial", 12, FontStyle.Bold),
                TextAlignment = TextAlignment.MiddleCenter
            });
            panel.Controls.Add(new XRLabel
            {
                Text = $"{ticket.TicketTypeName} | {order.ServiceDate:yyyy-MM-dd}",
                BoundsF = new RectangleF(10, 248, width - 20, 22),
                Font = new Font("Arial", 9),
                TextAlignment = TextAlignment.MiddleCenter
            });
            panel.Controls.Add(new XRLabel
            {
                Text = $"Valid: {ticket.ValidFrom:yyyy-MM-dd HH:mm} - {ticket.ValidTo:yyyy-MM-dd HH:mm}",
                BoundsF = new RectangleF(10, 272, width - 20, 20),
                Font = new Font("Arial", 8),
                TextAlignment = TextAlignment.MiddleCenter
            });
        }

        private async Task<byte[]?> LoadLogoBytesAsync(
            HotelSettings hotel,
            ResortScope scope,
            CancellationToken cancellationToken)
        {
            var hotelCode = hotel.HotelCode ?? scope.ScopeHotelId.ToString();
            return await _assetCache.GetLogoBytesAsync(hotelCode, hotel.LogoUrl, cancellationToken);
        }

        /// <summary>
        /// Signed ticket payloads (RSRT2.hotel.zaaer.sig) include dots; Byte compaction is required.
        /// </summary>
        private static void ConfigureResortTicketQrSymbology(XRBarCode barcode)
        {
            if (barcode.Symbology is not QRCodeGenerator qr)
            {
                barcode.Symbology = new QRCodeGenerator();
                qr = (QRCodeGenerator)barcode.Symbology;
            }

            qr.CompactionMode = QRCodeCompactionMode.Byte;
            qr.ErrorCorrectionLevel = QRCodeErrorCorrectionLevel.Q;
            qr.IncludeQuietZone = true;
        }

        private static bool TryCreateImage(byte[]? bytes, out Image? image)
        {
            image = null;
            if (bytes == null || bytes.Length == 0)
            {
                return false;
            }

            try
            {
                using var stream = new System.IO.MemoryStream(bytes);
                using var loaded = Image.FromStream(stream);
                image = new Bitmap(loaded);
                return true;
            }
            catch
            {
                image = null;
                return false;
            }
        }

        private async Task<HotelPricingTaxConfig> ResolveTicketTaxConfigAsync(ResortScope scope, CancellationToken cancellationToken)
        {
            foreach (var hotelId in new[] { scope.ScopeHotelId, scope.LocalHotelId })
            {
                var hasTaxes = await _context.Taxes.AsNoTracking()
                    .AnyAsync(t => t.HotelId == hotelId && t.Enabled, cancellationToken);
                if (hasTaxes)
                {
                    var config = await HotelPricingTaxHelper.GetPosConfigAsync(_context, hotelId, cancellationToken);
                    return config.VatRate > 0m
                        ? config
                        : config with { VatRate = 15m, VatIncluded = true };
                }
            }

            var fallback = await HotelPricingTaxHelper.GetPosConfigAsync(_context, scope.ScopeHotelId, cancellationToken);
            return fallback.VatRate > 0m
                ? fallback
                : fallback with { VatRate = 15m, VatIncluded = true };
        }

        private static bool IsCashPaymentMethod(PaymentMethod paymentMethod)
        {
            var code = paymentMethod.MethodCode?.Trim().ToLowerInvariant() ?? string.Empty;
            var name = paymentMethod.MethodName?.Trim().ToLowerInvariant() ?? string.Empty;
            var nameAr = paymentMethod.MethodNameAr ?? string.Empty;
            return code.Contains("cash", StringComparison.Ordinal)
                   || name.Contains("cash", StringComparison.Ordinal)
                   || nameAr.Contains("نقد", StringComparison.Ordinal);
        }

        private async Task<PaymentMethod> ResolvePaymentMethodAsync(int paymentMethodId, CancellationToken cancellationToken)
        {
            return await _context.PaymentMethods
                .AsNoTracking()
                .FirstOrDefaultAsync(pm => pm.PaymentMethodId == paymentMethodId && pm.IsActive, cancellationToken)
                ?? throw new ArgumentException("Selected payment method was not found.");
        }

        /// <summary>
        /// Returns <c>banks.zaaer_id</c> for the default active bank (network / card receipts).
        /// </summary>
        private async Task<int?> ResolveDefaultBankZaaerIdAsync(CancellationToken cancellationToken)
        {
            var bank = await _context.Banks.AsNoTracking()
                .Where(b => b.IsActive && b.IsDefault && b.ZaaerId.HasValue && b.ZaaerId.Value > 0)
                .OrderBy(b => b.SortOrder)
                .ThenBy(b => b.BankId)
                .FirstOrDefaultAsync(cancellationToken);
            if (bank?.ZaaerId is > 0)
            {
                return bank.ZaaerId;
            }

            bank = await _context.Banks.AsNoTracking()
                .Where(b => b.IsActive && b.ZaaerId.HasValue && b.ZaaerId.Value > 0)
                .OrderByDescending(b => b.IsDefault)
                .ThenBy(b => b.SortOrder)
                .ThenBy(b => b.BankId)
                .FirstOrDefaultAsync(cancellationToken);
            return bank?.ZaaerId;
        }

        private async Task<int?> ResolveReservationKeyAsync(int? reservationId, CancellationToken cancellationToken)
        {
            if (!reservationId.HasValue)
            {
                return null;
            }

            var reservation = await _context.Reservations.AsNoTracking()
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId.Value || r.ZaaerId == reservationId.Value, cancellationToken);
            return reservation?.ZaaerId ?? reservation?.ReservationId ?? reservationId;
        }

        private async Task<HotelSettings> LoadHotelAsync(ResortScope scope, CancellationToken cancellationToken)
        {
            return await _context.HotelSettings.AsNoTracking()
                .FirstAsync(h => h.HotelId == scope.LocalHotelId || h.ZaaerId == scope.ScopeHotelId, cancellationToken);
        }

        private async Task<ResortScope> GetCurrentResortScopeAsync(CancellationToken cancellationToken)
        {
            var tenant = _tenantService.GetTenant()
                ?? throw new UnauthorizedAccessException("Tenant not resolved. Provide X-Hotel-Code.");
            var code = tenant.Code.Trim();
            var hotel = await _context.HotelSettings.AsNoTracking()
                .Where(h => h.HotelCode != null)
                .FirstOrDefaultAsync(h => h.HotelCode!.ToLower() == code.ToLower(), cancellationToken)
                ?? throw new InvalidOperationException($"HotelSettings not found for code: {tenant.Code}");

            var propertyType = hotel.PropertyType?.Trim().ToLowerInvariant() ?? PropertyTypes.Hotel;
            if (!PropertyTypes.IsResort(propertyType))
            {
                throw new InvalidOperationException("Resort tickets are available only for resort properties.");
            }

            return new ResortScope(hotel.HotelId, hotel.ZaaerId ?? hotel.HotelId);
        }

        private sealed record ResortScope(int LocalHotelId, int ScopeHotelId);

        private async Task<ResortTicketConfig> GetOrCreateConfigEntityAsync(
            ResortScope scope,
            CancellationToken cancellationToken,
            bool tracking = false)
        {
            var query = _context.ResortTicketConfigs.Where(c => c.HotelId == scope.ScopeHotelId);
            if (!tracking)
            {
                query = query.AsNoTracking();
            }

            var config = await query.FirstOrDefaultAsync(cancellationToken);
            if (config != null)
            {
                return config;
            }

            config = new ResortTicketConfig
            {
                HotelId = scope.ScopeHotelId,
                IssueStartTime = new TimeSpan(16, 0, 0),
                TicketValidityEndTime = new TimeSpan(4, 0, 0),
                DailyCloseTime = new TimeSpan(4, 0, 0),
                CreatedAt = KsaTime.Now
            };
            _context.ResortTicketConfigs.Add(config);
            await _context.SaveChangesAsync(cancellationToken);
            if (!tracking)
            {
                _context.Entry(config).State = EntityState.Detached;
            }

            return config;
        }

        private static DateTime? NormalizeQueryDate(DateTime? value) =>
            value.HasValue ? KsaTime.ToGregorianBirthDateOnly(value) : null;

        private static PmsResortTicketBusinessConfigDto MapBusinessConfigDto(ResortTicketConfig config, DateTime nowKsa)
        {
            var businessDate = ResortTicketBusinessHours.ResolveCurrentBusinessServiceDate(
                nowKsa,
                config.IssueStartTime,
                config.DailyCloseTime);
            return new PmsResortTicketBusinessConfigDto
            {
                IssueStartTime = FormatTimeOfDay(config.IssueStartTime),
                TicketValidityEndTime = FormatTimeOfDay(config.TicketValidityEndTime),
                GamesValidityEndTime = config.GamesValidityEndTime.HasValue
                    ? FormatTimeOfDay(config.GamesValidityEndTime.Value)
                    : null,
                DailyCloseTime = FormatTimeOfDay(config.DailyCloseTime),
                CanIssueNow = ResortTicketBusinessHours.IsWithinIssueWindow(
                    nowKsa,
                    config.IssueStartTime,
                    config.DailyCloseTime),
                CurrentBusinessServiceDate = businessDate
            };
        }

        private async Task<IReadOnlyList<PmsResortTicketInvoiceListItemDto>> MapTicketInvoiceListAsync(
            IReadOnlyList<Invoice> invoices,
            CancellationToken cancellationToken)
        {
            if (invoices.Count == 0)
            {
                return Array.Empty<PmsResortTicketInvoiceListItemDto>();
            }

            var orderIds = invoices
                .Where(i => i.OrderId.HasValue)
                .Select(i => i.OrderId!.Value)
                .Distinct()
                .ToList();
            var orders = orderIds.Count == 0
                ? new Dictionary<int, ResortTicketOrder>()
                : await _context.ResortTicketOrders.AsNoTracking()
                    .Where(o => orderIds.Contains(o.TicketOrderId))
                    .ToDictionaryAsync(o => o.TicketOrderId, cancellationToken);

            var invoiceFks = invoices
                .Select(PmsInvoiceService.ResolveInvoiceForeignKey)
                .Distinct()
                .ToList();
            var creditNotes = await _context.CreditNotes.AsNoTracking()
                .Where(c => invoiceFks.Contains(c.InvoiceId))
                .ToListAsync(cancellationToken);
            var creditByInvoice = creditNotes
                .GroupBy(c => c.InvoiceId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.CreditNoteId).First());

            return invoices.Select(invoice =>
            {
                orders.TryGetValue(invoice.OrderId ?? 0, out var order);
                creditByInvoice.TryGetValue(PmsInvoiceService.ResolveInvoiceForeignKey(invoice), out var credit);
                var sentToZatca = invoice.IsSentZatca
                    || PmsInvoiceService.IsZatcaSubmitted(invoice.ZatcaStatus, invoice.ZatcaUuid);
                var cnSent = credit != null
                    && (credit.IsSentZatca || PmsInvoiceService.IsZatcaSubmitted(credit.ZatcaStatus, credit.ZatcaUuid));
                return new PmsResortTicketInvoiceListItemDto
                {
                    InvoiceId = invoice.InvoiceId,
                    InvoiceZaaerId = invoice.ZaaerId ?? invoice.InvoiceId,
                    InvoiceNo = invoice.InvoiceNo ?? string.Empty,
                    TicketOrderId = invoice.OrderId,
                    OrderNo = order?.OrderNo,
                    InvoiceDate = invoice.InvoiceDate,
                    TotalAmount = invoice.TotalAmount ?? 0m,
                    PaymentStatus = invoice.PaymentStatus ?? string.Empty,
                    ZatcaStatus = invoice.ZatcaStatus,
                    SentToZatca = sentToZatca,
                    CreditNoteNo = credit?.CreditNoteNo,
                    CreditNoteId = credit?.CreditNoteId,
                    CreditNoteZatcaStatus = credit?.ZatcaStatus,
                    CreditNoteSentToZatca = cnSent
                };
            }).ToList();
        }

        private static TimeSpan ParseTimeOfDay(string value)
        {
            if (TimeSpan.TryParse(value, out var parsed))
            {
                return parsed;
            }

            throw new ArgumentException($"Invalid time value: {value}");
        }

        private static string FormatTimeOfDay(TimeSpan value) =>
            $"{(int)value.TotalHours:D2}:{value.Minutes:D2}";

        /// <summary>
        /// Resolves an order by internal <see cref="ResortTicketOrder.TicketOrderId"/> only.
        /// Do not match on <see cref="ResortTicketOrder.ZaaerId"/> here — it collides with other orders'
        /// primary keys (e.g. ticket_order_id 7 vs zaaer_id 7 on RSTO0007).
        /// </summary>
        private async Task<ResortTicketOrder?> FindOrderByTicketOrderIdAsync(
            ResortScope scope,
            int ticketOrderId,
            bool tracking,
            CancellationToken cancellationToken)
        {
            var query = _context.ResortTicketOrders
                .Where(o => (o.HotelId == scope.ScopeHotelId || o.HotelId == scope.LocalHotelId)
                    && o.TicketOrderId == ticketOrderId);
            if (!tracking)
            {
                query = query.AsNoTracking();
            }

            return await query.FirstOrDefaultAsync(cancellationToken);
        }

        private static PmsResortTicketTypeDto MapTicketType(ResortTicketType row) => new()
        {
            TicketTypeId = row.TicketTypeId,
            ZaaerId = row.ZaaerId,
            Code = row.Code,
            NameAr = row.NameAr,
            NameEn = row.NameEn,
            Description = row.Description,
            UnitPrice = row.UnitPrice,
            VatRate = row.VatRate,
            ValidForHours = row.ValidForHours,
            ValidForMinutes = ResolveValidForMinutes(row),
            ValidityMode = row.ValidityMode,
            TicketCategory = row.TicketCategory,
            SortOrder = row.SortOrder,
            IsGeneric = row.IsGeneric,
            IsActive = row.IsActive
        };

        private static PmsResortTicketDto MapTicket(ResortTicket ticket, IReadOnlyDictionary<int, ResortTicketType> types)
        {
            types.TryGetValue(ticket.TicketTypeId, out var type);
            return new PmsResortTicketDto
            {
                TicketId = ticket.TicketId,
                ZaaerId = ticket.ZaaerId,
                TicketOrderId = ticket.TicketOrderId,
                TicketTypeId = ticket.TicketTypeId,
                TicketTypeName = type?.NameAr ?? type?.NameEn ?? ticket.TicketTypeId.ToString(),
                TicketNo = ticket.TicketNo,
                QrCode = ticket.QrCode,
                TicketStatus = ticket.TicketStatus,
                UnitPrice = ticket.UnitPrice,
                VatAmount = ticket.VatAmount,
                TotalAmount = ticket.TotalAmount,
                ValidFrom = ticket.ValidFrom,
                ValidTo = ticket.ValidTo,
                PrintedAt = ticket.PrintedAt,
                UsedAt = ticket.UsedAt,
                SessionStartedAt = ticket.SessionStartedAt,
                ValidityMode = type?.ValidityMode,
                CancelledAt = ticket.CancelledAt
            };
        }

        private async Task<ResortTicketType?> ResolveStationTypeAsync(
            ResortScope scope,
            string? stationCode,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(stationCode))
            {
                return null;
            }

            var normalized = NormalizeCode(stationCode);
            if (normalized is "entry" or "games" or "pool")
            {
                return null;
            }

            return await _context.ResortTicketTypes.AsNoTracking()
                .FirstOrDefaultAsync(
                    t => (t.HotelId == scope.ScopeHotelId || t.HotelId == scope.LocalHotelId)
                        && t.Code == normalized
                        && t.IsActive,
                    cancellationToken);
        }

        private static string? EvaluateStationMatch(
            ResortTicketType? ticketType,
            string? stationCode,
            ResortTicketType? stationType)
        {
            if (string.IsNullOrWhiteSpace(stationCode) || ticketType == null)
            {
                return null;
            }

            var station = NormalizeCode(stationCode);
            if (station == ResortTicketCategories.Entry)
            {
                return string.Equals(ticketType.TicketCategory, ResortTicketCategories.Entry, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : "wrong_station";
            }

            if (station == ResortTicketCategories.Games)
            {
                return string.Equals(ticketType.TicketCategory, ResortTicketCategories.Games, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : "wrong_station";
            }

            if (station == ResortTicketCategories.Pool)
            {
                return string.Equals(ticketType.TicketCategory, ResortTicketCategories.Pool, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : "wrong_station";
            }

            if (stationType == null)
            {
                return "unknown_station";
            }

            if (string.Equals(ticketType.Code, stationType.Code, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (ticketType.IsGeneric
                && string.Equals(ticketType.TicketCategory, stationType.TicketCategory, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return "wrong_station";
        }

        private static string NormalizeCode(string value) =>
            value.Trim().ToLowerInvariant().Replace(' ', '_');

        private static int ToInt32(long value)
        {
            if (value > int.MaxValue)
            {
                throw new InvalidOperationException("Generated Zaaer id is outside int range.");
            }

            return (int)value;
        }

        private static int? ToNullableInt32(long? value) => value.HasValue ? ToInt32(value.Value) : null;

        private static string AppendNote(string? current, string note) =>
            string.IsNullOrWhiteSpace(current) ? note : $"{current}{Environment.NewLine}{note}";
    }
}
