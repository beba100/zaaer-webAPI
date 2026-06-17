using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Security;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    public sealed class PmsPosOrderService : IPmsPosOrderService
    {
        private readonly ApplicationDbContext _context;
        private readonly MasterDbContext _masterDb;
        private readonly ITenantService _tenantService;
        private readonly ICurrentUserContext _currentUser;
        private readonly INumberingService _numberingService;
        private readonly IReservationFinancialSyncService _financialSync;

        public PmsPosOrderService(
            ApplicationDbContext context,
            MasterDbContext masterDb,
            ITenantService tenantService,
            ICurrentUserContext currentUser,
            INumberingService numberingService,
            IReservationFinancialSyncService financialSync)
        {
            _context = context;
            _masterDb = masterDb;
            _tenantService = tenantService;
            _currentUser = currentUser;
            _numberingService = numberingService;
            _financialSync = financialSync;
        }

        private async Task<HotelSettings> GetCurrentHotelSettingsAsync(CancellationToken cancellationToken)
        {
            var tenant = _tenantService.GetTenant()
                ?? throw new UnauthorizedAccessException("Tenant not resolved. Provide X-Hotel-Code.");

            return await _context.HotelSettings.AsNoTracking()
                .FirstOrDefaultAsync(h => h.HotelCode == tenant.Code, cancellationToken)
                ?? throw new InvalidOperationException($"HotelSettings not found for code: {tenant.Code}");
        }

        /// <summary>
        /// Outlets/tables use <c>hotel_settings.hotel_id</c>; reservations often store Zaaer property id in <c>hotel_id</c>.
        /// </summary>
        private async Task<int> GetCurrentHotelIdAsync(CancellationToken cancellationToken)
        {
            var hotel = await GetCurrentHotelSettingsAsync(cancellationToken);
            return hotel.HotelId;
        }

        private static List<int> BuildReservationHotelScopeIds(HotelSettings hotel)
        {
            var ids = new List<int> { hotel.HotelId };
            if (hotel.ZaaerId is > 0 && !ids.Contains(hotel.ZaaerId.Value))
            {
                ids.Add(hotel.ZaaerId.Value);
            }

            return ids;
        }

        /// <summary>Global Zaaer property id stored on orders, receipts, invoices, and reservations.</summary>
        private static int ResolveIntegrationHotelId(HotelSettings hotel) =>
            hotel.ZaaerId is > 0 ? hotel.ZaaerId.Value : hotel.HotelId;

        private async Task<HotelPricingTaxConfig> ResolvePosTaxConfigAsync(
            HotelSettings hotel,
            CancellationToken cancellationToken)
        {
            foreach (var hotelId in BuildReservationHotelScopeIds(hotel))
            {
                var hasTaxes = await _context.Taxes.AsNoTracking()
                    .AnyAsync(t => t.HotelId == hotelId && t.Enabled, cancellationToken);
                if (hasTaxes)
                {
                    return await HotelPricingTaxHelper.GetPosConfigAsync(_context, hotelId, cancellationToken);
                }
            }

            return await HotelPricingTaxHelper.GetPosConfigAsync(
                _context,
                ResolveIntegrationHotelId(hotel),
                cancellationToken);
        }

        private static (decimal UnitPriceNet, decimal TotalPriceGross) SplitPosLineAmounts(
            decimal unitPriceGross,
            decimal quantity,
            decimal discount,
            HotelPricingTaxConfig taxConfig)
        {
            var qty = quantity <= 0 ? 1m : quantity;
            var gross = Math.Round(unitPriceGross * qty - discount, 2, MidpointRounding.AwayFromZero);
            if (gross <= 0m)
            {
                return (0m, 0m);
            }

            var calc = HotelPricingTaxHelper.CalculateAmounts(gross, taxConfig);
            var unitNet = Math.Round(calc.NetAmount / qty, 2, MidpointRounding.AwayFromZero);
            return (unitNet, gross);
        }

        private static PmsPosPricingTaxDto MapTaxDto(HotelPricingTaxConfig config) =>
            new()
            {
                VatRate = config.VatRate,
                EwaRate = config.EwaRate,
                VatTaxIncluded = config.VatIncluded,
                LodgingTaxIncluded = config.EwaIncluded
            };

        private static IEnumerable<decimal> BuildLineGrossAmounts(IEnumerable<PmsPosOrderLineDto> lines)
        {
            foreach (var line in lines)
            {
                var qty = line.Quantity <= 0 ? 1 : line.Quantity;
                yield return Math.Round(line.UnitPrice * qty - line.Discount, 2, MidpointRounding.AwayFromZero);
            }
        }

        public async Task<PmsPosOrderDto> CreateOrderAsync(PmsCreatePosOrderDto dto, CancellationToken cancellationToken = default)
        {
            if (dto.Lines == null || dto.Lines.Count == 0)
            {
                throw new ArgumentException("At least one order line is required.");
            }

            if (dto.DiscountAmount > 0 && !_currentUser.HasPermission("pos.orders.discount"))
            {
                throw new UnauthorizedAccessException("You do not have permission to apply a POS order discount.");
            }

            var hotel = await GetCurrentHotelSettingsAsync(cancellationToken);
            var hotelId = hotel.HotelId;
            var hotelScopeIds = BuildReservationHotelScopeIds(hotel);
            var outlet = await _context.Outlets.AsNoTracking()
                .FirstOrDefaultAsync(o => o.OutletId == dto.OutletId && o.HotelId == hotelId && o.IsActive, cancellationToken)
                ?? throw new ArgumentException("Outlet not found or inactive.");

            if (dto.TableId.HasValue)
            {
                var tableOk = await _context.OutletTables.AnyAsync(
                    t => t.TableId == dto.TableId.Value && t.OutletId == dto.OutletId && t.HotelId == hotelId && t.IsActive,
                    cancellationToken);
                if (!tableOk)
                {
                    throw new ArgumentException("Table not found for this outlet.");
                }
            }

            var lineGross = BuildLineGrossAmounts(dto.Lines).ToList();
            var grossSum = Math.Round(lineGross.Sum(), 2, MidpointRounding.AwayFromZero);
            if (dto.DiscountAmount > grossSum)
            {
                throw new ArgumentException("Discount cannot exceed the order total.");
            }

            var integrationHotelId = ResolveIntegrationHotelId(hotel);
            var taxConfig = await ResolvePosTaxConfigAsync(hotel, cancellationToken);
            var (subtotal, tax, total) = HotelPricingTaxHelper.ComputePosOrderTotals(
                lineGross,
                dto.DiscountAmount,
                taxConfig);

            var paid = dto.Payments?.Sum(p => p.Amount) ?? 0;
            paid = Math.Round(paid, 2, MidpointRounding.AwayFromZero);

            Reservation? linkedReservation = null;
            int? linkedReservationStorageId = null;
            int? linkedCustomerStorageId = null;
            var chargeToReservation = dto.ReservationId is > 0;

            if (chargeToReservation)
            {
                linkedReservation = await PmsReservationRouteResolver.FindAsync(
                    _context,
                    dto.ReservationId!.Value,
                    hotelId: null,
                    asNoTracking: true,
                    cancellationToken)
                    ?? throw new ArgumentException("Reservation not found.");

                if (!hotelScopeIds.Contains(linkedReservation.HotelId))
                {
                    throw new ArgumentException("Reservation not found.");
                }

                if (!IsCheckedInReservationStatus(linkedReservation.Status))
                {
                    throw new ArgumentException("pos.terminal.reservationNotInHouse");
                }

                if (!linkedReservation.ZaaerId.HasValue || linkedReservation.ZaaerId.Value <= 0)
                {
                    throw new ArgumentException("Reservation ZaaerId is required to charge POS orders.");
                }

                linkedReservationStorageId = linkedReservation.ZaaerId.Value;
                linkedCustomerStorageId = await ResolveCustomerStorageIdAsync(
                    hotelScopeIds,
                    linkedReservation.CustomerId,
                    cancellationToken)
                    ?? throw new ArgumentException("Reservation guest is required to charge POS orders.");
            }

            if (!chargeToReservation)
            {
                if (paid > 0 && paid < total)
                {
                    throw new ArgumentException("Payment amount must cover the order total.");
                }

                if (dto.Payments != null && dto.Payments.Count > 0 && paid < total)
                {
                    throw new ArgumentException("Payment amount must equal the order total.");
                }
            }

            var isDirectPaid = !chargeToReservation && paid >= total && total > 0;
            if (isDirectPaid)
            {
                paid = total;
            }

            string paymentStatus;
            string orderStatus;
            if (chargeToReservation)
            {
                paymentStatus = "transferred_to_reservation";
                orderStatus = "transferred_to_reservation";
            }
            else if (isDirectPaid)
            {
                paymentStatus = "Paid";
                orderStatus = "paid";
            }
            else
            {
                paymentStatus = paid <= 0 ? "unpaid" : "partial";
                orderStatus = "created";
            }

            const string orderType = "outlet";
            var orderPaidAmount = chargeToReservation ? 0m : isDirectPaid ? total : paid;

            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var identity = await _numberingService.GetNextBusinessIdentityAsync(
                    "order",
                    hotelId,
                    PmsCurrentUser.ResolveDisplayName(_currentUser),
                    $"pms-pos-order:{hotelId}:{Guid.NewGuid():N}",
                    cancellationToken);

                var now = KsaTime.Now;
                var pmsUserId = PmsCurrentUser.ResolveUserId(_currentUser);
                var order = new Order
                {
                    ZaaerId = ZaaerIdMapper.ToNullableInt32(identity.ZaaerId),
                    OrderNo = identity.DocumentNo,
                    HotelId = integrationHotelId,
                    OutletId = dto.OutletId,
                    TableId = dto.TableId,
                    OrderDate = now.Date,
                    OrderTime = now.ToString("HH:mm"),
                    OrderStatus = orderStatus,
                    PaymentStatus = paymentStatus,
                    OrderType = orderType,
                    ReservationId = linkedReservationStorageId,
                    CustomerId = linkedCustomerStorageId,
                    Subtotal = subtotal,
                    TaxAmount = tax,
                    DiscountAmount = dto.DiscountAmount,
                    TotalAmount = total,
                    PaidAmount = orderPaidAmount,
                    Balance = chargeToReservation || isDirectPaid
                        ? 0
                        : Math.Round(total - paid, 2, MidpointRounding.AwayFromZero),
                    Target = dto.GuestName?.Trim(),
                    Notes = dto.Notes?.Trim(),
                    CreatedBy = pmsUserId,
                    CreatedAt = now
                };

                await _context.Orders.AddAsync(order, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                var orderItemLinkId = order.ZaaerId ?? order.OrderId;
                foreach (var line in dto.Lines)
                {
                    var qtyDec = line.Quantity <= 0 ? 1 : line.Quantity;
                    var qtyInt = Math.Max(1, (int)Math.Ceiling(qtyDec));
                    var (unitNet, lineGrossTotal) = SplitPosLineAmounts(
                        line.UnitPrice,
                        qtyDec,
                        line.Discount,
                        taxConfig);
                    await _context.OrderItems.AddAsync(new OrderItem
                    {
                        OrderId = orderItemLinkId,
                        ItemId = line.ItemId,
                        ItemName = line.ItemName.Trim(),
                        Quantity = qtyInt,
                        UnitPrice = unitNet,
                        Discount = line.Discount,
                        TotalPrice = lineGrossTotal,
                        CreatedAt = now
                    }, cancellationToken);
                }

                await _context.SaveChangesAsync(cancellationToken);

                if (chargeToReservation && linkedReservation != null)
                {
                    await ApplyPosTransferToReservationAsync(
                        linkedReservation,
                        order,
                        subtotal,
                        tax,
                        total,
                        dto.Lines,
                        pmsUserId,
                        cancellationToken);
                }
                else if (isDirectPaid && dto.Payments != null && dto.Payments.Count > 0)
                {
                    var payment = dto.Payments[0];
                    await CreatePosReceiptAsync(
                        order,
                        integrationHotelId,
                        hotelId,
                        payment.PaymentMethodId,
                        total,
                        now,
                        pmsUserId,
                        payment.BankId,
                        payment.TransactionNo,
                        reservationStorageId: null,
                        customerStorageId: null,
                        cancellationToken);
                    await CreatePosSalesInvoiceAsync(
                        order,
                        integrationHotelId,
                        hotelId,
                        taxConfig,
                        pmsUserId,
                        cancellationToken);
                }

                await _numberingService.MarkCommittedAsync(identity.AuditId, cancellationToken);
                await tx.CommitAsync(cancellationToken);

                return (await GetOrderAsync(order.OrderId, cancellationToken))!;
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        }

        private async Task<PaymentReceipt> CreatePosReceiptAsync(
            Order order,
            int integrationHotelId,
            int numberingHotelId,
            int? paymentMethodId,
            decimal amount,
            DateTime receiptDate,
            int? createdBy,
            int? bankId,
            string? transactionNo,
            int? reservationStorageId,
            int? customerStorageId,
            CancellationToken cancellationToken)
        {
            string? paymentMethodName = null;
            if (paymentMethodId is > 0)
            {
                paymentMethodName = await _context.PaymentMethods.AsNoTracking()
                    .Where(pm => pm.PaymentMethodId == paymentMethodId.Value)
                    .Select(pm => pm.MethodName)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            var receiptIdentity = await _numberingService.GetNextBusinessIdentityAsync(
                "payment_receipt",
                numberingHotelId,
                createdBy?.ToString() ?? "pms",
                $"pms-pos-receipt:{numberingHotelId}:{order.OrderId}:{Guid.NewGuid():N}",
                cancellationToken);

            var roundedAmount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
            var orderLinkId = ResolveOrderReceiptLinkId(order);
            var receipt = new PaymentReceipt
            {
                ReceiptNo = receiptIdentity.DocumentNo,
                ZaaerId = ZaaerIdMapper.ToNullableInt32(receiptIdentity.ZaaerId),
                HotelId = integrationHotelId,
                OrderId = orderLinkId,
                ReservationId = reservationStorageId,
                CustomerId = customerStorageId,
                ReceiptDate = receiptDate,
                ReceiptType = "receipt",
                VoucherCode = "receipt",
                AmountPaid = roundedAmount,
                PaymentMethodId = paymentMethodId is > 0 ? paymentMethodId : null,
                PaymentMethod = paymentMethodName,
                BankId = bankId,
                TransactionNo = transactionNo?.Trim() ?? string.Empty,
                Notes = reservationStorageId.HasValue
                    ? $"POS order {order.OrderNo} — charged to reservation"
                    : $"POS order {order.OrderNo}",
                Reason = reservationStorageId.HasValue
                    ? $"POS order {order.OrderNo} — reservation charge"
                    : $"POS order {order.OrderNo}",
                ReceiptStatus = "paid",
                RevenueCategory = orderLinkId > 0 ? "other" : null,
                AllocatedAmount = 0m,
                UnallocatedAmount = roundedAmount,
                IsFullyAllocated = false,
                CreatedBy = createdBy,
                CreatedAt = KsaTime.Now
            };

            await _context.PaymentReceipts.AddAsync(receipt, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await _numberingService.MarkCommittedAsync(receiptIdentity.AuditId, cancellationToken);
            return receipt;
        }

        private async Task<Invoice> CreatePosSalesInvoiceAsync(
            Order order,
            int integrationHotelId,
            int numberingHotelId,
            HotelPricingTaxConfig taxConfig,
            int? createdBy,
            CancellationToken cancellationToken)
        {
            var identity = await _numberingService.GetNextBusinessIdentityAsync(
                "invoice",
                numberingHotelId,
                createdBy?.ToString() ?? "pms",
                $"pms-pos-invoice:{numberingHotelId}:{order.OrderId}:{Guid.NewGuid():N}",
                cancellationToken);

            var orderLinkId = ResolveOrderReceiptLinkId(order);
            var total = Math.Round(order.TotalAmount ?? 0m, 2, MidpointRounding.AwayFromZero);
            var taxAmount = Math.Round(order.TaxAmount ?? 0m, 2, MidpointRounding.AwayFromZero);
            var subtotal = Math.Round(total - taxAmount, 2, MidpointRounding.AwayFromZero);

            var invoice = new Invoice
            {
                InvoiceNo = identity.DocumentNo,
                ZaaerId = ZaaerIdMapper.ToNullableInt32(identity.ZaaerId),
                HotelId = integrationHotelId,
                OrderId = orderLinkId,
                InvoiceDate = order.OrderDate,
                InvoiceType = "sales_invoice",
                TotalAmount = total,
                Subtotal = subtotal,
                VatRate = taxConfig.VatRate,
                VatAmount = taxAmount,
                LodgingTaxRate = 0m,
                LodgingTaxAmount = 0m,
                PaymentStatus = "paid",
                AmountPaid = 0m,
                AmountRemaining = 0m,
                IsSentZatca = false,
                ZatcaStatus = "pending",
                ZatcaUuid = identity.DocumentNo,
                RevenueCategory = "other",
                Notes = $"POS order {order.OrderNo}",
                CreatedBy = createdBy,
                CreatedAt = KsaTime.Now
            };

            await _context.Invoices.AddAsync(invoice, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await _numberingService.MarkCommittedAsync(identity.AuditId, cancellationToken);
            return invoice;
        }

        public async Task<IReadOnlyList<PmsPosInHouseReservationDto>> ListInHouseReservationsAsync(
            CancellationToken cancellationToken = default)
        {
            var hotel = await GetCurrentHotelSettingsAsync(cancellationToken);
            var hotelScopeIds = BuildReservationHotelScopeIds(hotel);

            var reservations = await _context.Reservations.AsNoTracking()
                .Where(r => hotelScopeIds.Contains(r.HotelId))
                .ToListAsync(cancellationToken);

            var inHouse = reservations
                .Where(r => IsCheckedInReservationStatus(r.Status) && r.ZaaerId is > 0)
                .OrderBy(r => r.ReservationNo)
                .ToList();

            if (inHouse.Count == 0)
            {
                return Array.Empty<PmsPosInHouseReservationDto>();
            }

            var zaaerReservationIds = inHouse
                .Select(r => r.ZaaerId!.Value)
                .Distinct()
                .ToList();

            var units = await _context.ReservationUnits.AsNoTracking()
                .Where(u => zaaerReservationIds.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);

            var customerZaaerIds = inHouse
                .Where(r => r.CustomerId is > 0)
                .Select(r => r.CustomerId!.Value)
                .Distinct()
                .ToList();

            var apartmentIds = units.Select(u => u.ApartmentId).Distinct().ToList();
            var apartments = apartmentIds.Count == 0
                ? new List<Apartment>()
                : await _context.Apartments.AsNoTracking()
                    .Where(a =>
                        hotelScopeIds.Contains(a.HotelId)
                        && (apartmentIds.Contains(a.ApartmentId)
                            || (a.ZaaerId != null && apartmentIds.Contains(a.ZaaerId.Value))))
                    .ToListAsync(cancellationToken);

            var customers = customerZaaerIds.Count == 0
                ? new List<Customer>()
                : await _context.Customers.AsNoTracking()
                    .Where(c =>
                        hotelScopeIds.Contains(c.HotelId)
                        && c.ZaaerId != null
                        && customerZaaerIds.Contains(c.ZaaerId.Value))
                    .ToListAsync(cancellationToken);

            var result = new List<PmsPosInHouseReservationDto>(inHouse.Count);
            foreach (var reservation in inHouse)
            {
                var resUnits = units
                    .Where(u => u.ReservationId == reservation.ZaaerId!.Value)
                    .ToList();

                var roomLabels = new List<string>();
                foreach (var unit in resUnits)
                {
                    var apt = apartments.FirstOrDefault(a =>
                        a.ApartmentId == unit.ApartmentId
                        || (a.ZaaerId is > 0 && a.ZaaerId.Value == unit.ApartmentId));
                    var label = apt != null
                        ? (string.IsNullOrWhiteSpace(apt.ApartmentCode)
                            ? apt.ApartmentName
                            : apt.ApartmentCode)
                        : null;
                    if (!string.IsNullOrWhiteSpace(label))
                    {
                        roomLabels.Add(label!.Trim());
                    }
                }

                var customer = reservation.CustomerId is > 0
                    ? customers.FirstOrDefault(c => c.ZaaerId == reservation.CustomerId.Value)
                    : null;
                var customerName = customer?.CustomerName?.Trim() ?? string.Empty;
                var rooms = string.Join(", ", roomLabels.Distinct(StringComparer.OrdinalIgnoreCase));
                var display = string.Join(
                    " · ",
                    new[] { reservation.ReservationNo, rooms, customerName }
                        .Where(s => !string.IsNullOrWhiteSpace(s)));

                result.Add(new PmsPosInHouseReservationDto
                {
                    ReservationId = reservation.ZaaerId!.Value,
                    ReservationNo = reservation.ReservationNo,
                    CustomerId = customer?.ZaaerId ?? reservation.CustomerId,
                    CustomerName = customerName,
                    RoomLabels = rooms,
                    DisplayLabel = display
                });
            }

            return result;
        }

        private async Task ApplyPosTransferToReservationAsync(
            Reservation reservation,
            Order order,
            decimal subtotal,
            decimal taxAmount,
            decimal totalAmount,
            IReadOnlyList<PmsPosOrderLineDto> lines,
            int? createdBy,
            CancellationToken cancellationToken)
        {
            var storeReservationId = reservation.ZaaerId is > 0
                ? reservation.ZaaerId.Value
                : reservation.ReservationId;

            var grossTotal = Math.Round(order.TotalAmount ?? totalAmount, 2, MidpointRounding.AwayFromZero);
            var netSubtotal = Math.Round(order.Subtotal ?? subtotal, 2, MidpointRounding.AwayFromZero);
            var tax = Math.Round(order.TaxAmount ?? taxAmount, 2, MidpointRounding.AwayFromZero);

            await _context.ReservationExtras.AddAsync(
                new ReservationExtra
                {
                    ReservationId = storeReservationId,
                    ItemName = PosReservationExtraNaming.BuildItemName(order.OrderNo),
                    PostingRule = "OnCustomDate",
                    ServiceDate = order.OrderDate,
                    GuestCount = 1,
                    NightCount = 1,
                    UnitPrice = grossTotal,
                    Subtotal = netSubtotal,
                    TaxAmount = tax,
                    TotalAmount = grossTotal,
                    CreatedBy = createdBy,
                    CreatedAt = KsaTime.Now
                },
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await RecalculateReservationTotalsAfterPosExtraAsync(reservation.ReservationId, cancellationToken);
        }

        private async Task RecalculateReservationTotalsAfterPosExtraAsync(
            int internalReservationId,
            CancellationToken cancellationToken)
        {
            var reservation = await _context.Reservations
                .FirstOrDefaultAsync(r => r.ReservationId == internalReservationId, cancellationToken);
            if (reservation == null)
            {
                return;
            }

            var keys = ReservationFinancialSyncService.BuildReservationKeys(
                reservation.ReservationId,
                reservation.ZaaerId);
            var units = await _context.ReservationUnits
                .Where(u => keys.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);
            var extras = await _context.ReservationExtras
                .Where(e => keys.Contains(e.ReservationId))
                .ToListAsync(cancellationToken);

            var sumUnitsTotal = units.Sum(u => u.TotalAmount);
            var sumExtrasTotal = Math.Round(extras.Sum(e => e.TotalAmount), 2, MidpointRounding.AwayFromZero);
            reservation.TotalExtra = sumExtrasTotal;
            reservation.TotalAmount = Math.Round(
                sumUnitsTotal + sumExtrasTotal + (reservation.TotalPenalties ?? 0m),
                2,
                MidpointRounding.AwayFromZero);

            await _context.SaveChangesAsync(cancellationToken);
            await _financialSync.SyncReservationRentPaymentTotalsAsync(internalReservationId, cancellationToken);

            reservation = await _context.Reservations
                .FirstOrDefaultAsync(r => r.ReservationId == internalReservationId, cancellationToken);
            if (reservation == null)
            {
                return;
            }

            reservation.BalanceAmount = Math.Round(
                reservation.TotalAmount.GetValueOrDefault()
                - (reservation.TotalDiscounts ?? 0m)
                - reservation.AmountPaid.GetValueOrDefault(),
                2,
                MidpointRounding.AwayFromZero);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task RemovePosTransferExtraAsync(
            Reservation reservation,
            Order order,
            CancellationToken cancellationToken)
        {
            var keys = ReservationFinancialSyncService.BuildReservationKeys(
                reservation.ReservationId,
                reservation.ZaaerId);
            var itemName = PosReservationExtraNaming.BuildItemName(order.OrderNo);

            var extras = await _context.ReservationExtras
                .Where(e => keys.Contains(e.ReservationId)
                    && e.ItemName == itemName)
                .ToListAsync(cancellationToken);

            if (extras.Count > 0)
            {
                _context.ReservationExtras.RemoveRange(extras);
                await _context.SaveChangesAsync(cancellationToken);
            }

            await RecalculateReservationTotalsAfterPosExtraAsync(reservation.ReservationId, cancellationToken);
        }

        private static int ResolveOrderReceiptLinkId(Order order) =>
            order.ZaaerId is > 0 ? order.ZaaerId.Value : order.OrderId;

        private async Task<int?> ResolveCustomerStorageIdAsync(
            IReadOnlyList<int> hotelScopeIds,
            int? customerRouteId,
            CancellationToken cancellationToken)
        {
            if (!customerRouteId.HasValue || customerRouteId.Value <= 0)
            {
                return null;
            }

            var customer = await _context.Customers.AsNoTracking()
                .FirstOrDefaultAsync(
                    c =>
                        hotelScopeIds.Contains(c.HotelId)
                        && c.ZaaerId == customerRouteId.Value,
                    cancellationToken);

            if (customer == null || PmsCustomerMarkers.IsDraftPlaceholder(customer))
            {
                return null;
            }

            return customer.ZaaerId is > 0 ? customer.ZaaerId.Value : customer.CustomerId;
        }

        private static bool IsCheckedInReservationStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            var norm = status.Trim().ToLowerInvariant()
                .Replace(" ", "", StringComparison.Ordinal)
                .Replace("-", "", StringComparison.Ordinal)
                .Replace("_", "", StringComparison.Ordinal);
            return norm is "checkedin" or "checkin";
        }

        private async Task<PaymentReceipt?> FindPrimaryPosReceiptAsync(int orderId, CancellationToken cancellationToken)
        {
            var order = await _context.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);
            if (order == null)
            {
                return null;
            }

            var orderLinkId = ResolveOrderReceiptLinkId(order);
            return await _context.PaymentReceipts.AsNoTracking()
                .Where(pr =>
                    pr.OrderId == orderLinkId
                    && pr.VoucherCode == "receipt"
                    && pr.ReceiptType == "receipt")
                .OrderByDescending(pr => pr.ReceiptId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private static bool OrderReceiptMatches(PaymentReceipt receipt, Order order)
        {
            if (!receipt.OrderId.HasValue)
            {
                return false;
            }

            var linkId = ResolveOrderReceiptLinkId(order);
            return receipt.OrderId.Value == linkId || receipt.OrderId.Value == order.OrderId;
        }

        public async Task<PmsPosOrderDto?> GetOrderAsync(int orderId, CancellationToken cancellationToken = default)
        {
            var hotel = await GetCurrentHotelSettingsAsync(cancellationToken);
            var hotelScopeIds = BuildReservationHotelScopeIds(hotel);
            var order = await _context.Orders.AsNoTracking()
                .Include(o => o.Outlet)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && hotelScopeIds.Contains(o.HotelId), cancellationToken);

            if (order == null)
            {
                return null;
            }

            var orderLinkId = ResolveOrderReceiptLinkId(order);
            var lines = await _context.OrderItems.AsNoTracking()
                .Where(i => i.OrderId == orderLinkId || i.OrderId == orderId)
                .OrderBy(i => i.OrderItemId)
                .Select(i => new PmsPosOrderLineDto
                {
                    ItemId = i.ItemId,
                    ItemName = i.ItemName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Discount = i.Discount,
                    TotalLineGross = i.TotalPrice
                })
                .ToListAsync(cancellationToken);

            var receipt = await FindPrimaryPosReceiptAsync(orderId, cancellationToken);
            return MapOrder(order, lines, receipt);
        }

        public async Task<IReadOnlyList<PmsPosOrderDto>> ListRecentOrdersAsync(int? outletId, int take, CancellationToken cancellationToken = default)
        {
            var rows = await ListOrdersAsync(outletId, take, cancellationToken);
            var result = new List<PmsPosOrderDto>(rows.Count);
            foreach (var row in rows)
            {
                var full = await GetOrderAsync(row.OrderId, cancellationToken);
                if (full != null)
                {
                    result.Add(full);
                }
            }

            return result;
        }

        public async Task<IReadOnlyList<PmsPosOrderListItemDto>> ListOrdersAsync(
            int? outletId,
            int take,
            CancellationToken cancellationToken = default)
        {
            var hotel = await GetCurrentHotelSettingsAsync(cancellationToken);
            var hotelScopeIds = BuildReservationHotelScopeIds(hotel);
            take = Math.Clamp(take, 1, 500);

            var orders = await _context.Orders.AsNoTracking()
                .Include(o => o.Outlet)
                .Where(o => hotelScopeIds.Contains(o.HotelId)
                    && (o.OrderType == "outlet" || o.OrderType == "ForReservation")
                    && (!outletId.HasValue || o.OutletId == outletId))
                .OrderByDescending(o => o.CreatedAt)
                .ThenByDescending(o => o.OrderId)
                .Take(take)
                .ToListAsync(cancellationToken);

            var orderLinkIds = orders
                .Select(ResolveOrderReceiptLinkId)
                .Concat(orders.Select(o => o.OrderId))
                .Distinct()
                .ToList();
            var receipts = await _context.PaymentReceipts.AsNoTracking()
                .Where(pr => pr.OrderId != null && orderLinkIds.Contains(pr.OrderId.Value)
                    && pr.VoucherCode == "receipt"
                    && pr.ReceiptType == "receipt")
                .OrderByDescending(pr => pr.ReceiptId)
                .ToListAsync(cancellationToken);

            var receiptByOrder = new Dictionary<int, PaymentReceipt>();
            foreach (var order in orders)
            {
                var receipt = receipts.FirstOrDefault(r => OrderReceiptMatches(r, order));
                if (receipt != null)
                {
                    receiptByOrder[order.OrderId] = receipt;
                }
            }

            var creatorIds = orders
                .Where(o => o.CreatedBy.HasValue && o.CreatedBy.Value > 0)
                .Select(o => o.CreatedBy!.Value)
                .Distinct()
                .ToList();

            var creators = creatorIds.Count == 0
                ? new Dictionary<int, (string Username, string FirstName, string LastName)>()
                : await _masterDb.RbacUsers.AsNoTracking()
                    .Where(u => creatorIds.Contains(u.UserId))
                    .Select(u => new { u.UserId, u.Username, u.FirstName, u.LastName })
                    .ToDictionaryAsync(
                        u => u.UserId,
                        u => (
                            Username: u.Username ?? string.Empty,
                            FirstName: u.FirstName ?? string.Empty,
                            LastName: u.LastName ?? string.Empty),
                        cancellationToken);

            var pmIds = receipts
                .Where(r => r.PaymentMethodId.HasValue && r.PaymentMethodId.Value > 0)
                .Select(r => r.PaymentMethodId!.Value)
                .Distinct()
                .ToList();

            var paymentMethodLabels = pmIds.Count == 0
                ? new Dictionary<int, (string En, string? Ar)>()
                : await _context.PaymentMethods.AsNoTracking()
                    .Where(pm => pmIds.Contains(pm.PaymentMethodId))
                    .Select(pm => new { pm.PaymentMethodId, pm.MethodName, pm.MethodNameAr })
                    .ToDictionaryAsync(
                        pm => pm.PaymentMethodId,
                        pm => (pm.MethodName, pm.MethodNameAr),
                        cancellationToken);

            var reservationRouteIds = orders
                .Where(o => o.ReservationId is > 0)
                .Select(o => o.ReservationId!.Value)
                .Distinct()
                .ToList();

            var reservationByRouteId = new Dictionary<int, (string ReservationNo, int RouteId, string? Status)>();
            if (reservationRouteIds.Count > 0)
            {
                var reservationRows = await _context.Reservations.AsNoTracking()
                    .Where(r =>
                        (r.ZaaerId != null && reservationRouteIds.Contains(r.ZaaerId.Value))
                        || reservationRouteIds.Contains(r.ReservationId))
                    .Select(r => new { r.ReservationId, r.ZaaerId, r.ReservationNo, r.Status })
                    .ToListAsync(cancellationToken);

                foreach (var row in reservationRows)
                {
                    var routeId = row.ZaaerId is > 0 ? row.ZaaerId.Value : row.ReservationId;
                    var entry = (row.ReservationNo, routeId, row.Status);
                    reservationByRouteId[row.ReservationId] = entry;
                    if (row.ZaaerId is > 0)
                    {
                        reservationByRouteId[row.ZaaerId.Value] = entry;
                    }
                }
            }

            return orders.Select(o =>
            {
                receiptByOrder.TryGetValue(o.OrderId, out var receipt);
                var cancelled = o.OrderStatus.Equals("cancelled", StringComparison.OrdinalIgnoreCase);
                var total = o.TotalAmount ?? 0;
                var displayAmount = cancelled ? -Math.Abs(total) : total;

                string? createdByName = null;
                string? createdByUsername = null;
                string? createdByFirstName = null;
                string? createdByLastName = null;
                if (o.CreatedBy.HasValue && creators.TryGetValue(o.CreatedBy.Value, out var creator))
                {
                    createdByUsername = creator.Username.Trim();
                    createdByFirstName = creator.FirstName.Trim();
                    createdByLastName = creator.LastName.Trim();
                    var full = $"{createdByFirstName} {createdByLastName}".Trim();
                    createdByName = !string.IsNullOrWhiteSpace(full)
                        ? full
                        : createdByUsername;
                }

                string? paymentMethod = receipt?.PaymentMethod;
                string? paymentMethodAr = null;
                if (receipt?.PaymentMethodId is > 0 && paymentMethodLabels.TryGetValue(receipt.PaymentMethodId.Value, out var pm))
                {
                    paymentMethod = pm.Item1;
                    paymentMethodAr = pm.Item2;
                }

                int? reservationRouteId = null;
                string? reservationNo = null;
                string? reservationStatus = null;
                var isTransferred = o.OrderStatus.Equals(
                    "transferred_to_reservation",
                    StringComparison.OrdinalIgnoreCase);
                var reservationEditable = false;
                if (o.ReservationId is > 0
                    && reservationByRouteId.TryGetValue(o.ReservationId.Value, out var resInfo))
                {
                    reservationRouteId = resInfo.RouteId;
                    reservationNo = resInfo.ReservationNo;
                    reservationStatus = resInfo.Status;
                    reservationEditable = IsCheckedInReservationStatus(resInfo.Status);
                }

                return new PmsPosOrderListItemDto
                {
                    OrderId = o.OrderId,
                    OrderNo = o.OrderNo,
                    ReservationId = reservationRouteId,
                    ReservationNo = reservationNo,
                    ReservationStatus = reservationStatus,
                    DisplayAmount = displayAmount,
                    TotalAmount = o.TotalAmount,
                    OutletId = o.OutletId,
                    OutletName = o.Outlet?.OutletName,
                    OutletNameAr = o.Outlet?.OutletNameAr,
                    CreatedByName = createdByName,
                    CreatedByUsername = createdByUsername,
                    CreatedByFirstName = createdByFirstName,
                    CreatedByLastName = createdByLastName,
                    OrderDate = o.OrderDate,
                    OrderTime = o.OrderTime,
                    CreatedAt = o.CreatedAt,
                    OrderStatus = o.OrderStatus,
                    PaymentStatus = o.PaymentStatus,
                    ReceiptId = receipt?.ReceiptId,
                    ReceiptNo = receipt?.ReceiptNo,
                    PaymentMethodId = receipt?.PaymentMethodId,
                    PaymentMethod = paymentMethod,
                    PaymentMethodAr = paymentMethodAr,
                    ReceiptBankId = receipt?.BankId,
                    ReceiptTransactionNo = receipt?.TransactionNo,
                    CanEditReceipt = receipt != null && !cancelled,
                    CanEditTransferred = isTransferred && reservationEditable && !cancelled,
                    CanCancel = !cancelled && (
                        o.OrderStatus.Equals("paid", StringComparison.OrdinalIgnoreCase)
                        || o.OrderStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase)
                        || (isTransferred && reservationEditable))
                };
            }).ToList();
        }

        public async Task<PmsPosOrderDto> UpdateOrderReceiptAsync(
            int orderId,
            PmsUpdatePosOrderReceiptDto dto,
            CancellationToken cancellationToken = default)
        {
            var hotel = await GetCurrentHotelSettingsAsync(cancellationToken);
            var hotelScopeIds = BuildReservationHotelScopeIds(hotel);
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderId == orderId && hotelScopeIds.Contains(o.HotelId), cancellationToken)
                ?? throw new ArgumentException("Order not found.");

            if (order.OrderStatus.Equals("cancelled", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Cannot edit receipt for a cancelled order.");
            }

            if (order.OrderStatus.Equals("transferred_to_reservation", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Cannot edit receipt for a reservation transfer order.");
            }

            var receipt = await FindPrimaryPosReceiptAsync(orderId, cancellationToken)
                ?? throw new ArgumentException("Payment receipt not found for this order.");

            string? paymentMethodName = null;
            if (dto.PaymentMethodId > 0)
            {
                paymentMethodName = await _context.PaymentMethods.AsNoTracking()
                    .Where(pm => pm.PaymentMethodId == dto.PaymentMethodId)
                    .Select(pm => pm.MethodName)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            var pmRow = await _context.PaymentMethods.AsNoTracking()
                .FirstOrDefaultAsync(pm => pm.PaymentMethodId == dto.PaymentMethodId, cancellationToken);
            var isCash = pmRow != null && (
                (pmRow.Category ?? string.Empty).Equals("Cash", StringComparison.OrdinalIgnoreCase)
                || (pmRow.MethodCode ?? string.Empty).Contains("cash", StringComparison.OrdinalIgnoreCase)
                || (pmRow.MethodName ?? string.Empty).Contains("cash", StringComparison.OrdinalIgnoreCase)
                || (pmRow.MethodNameAr ?? string.Empty).Contains("نقد", StringComparison.OrdinalIgnoreCase));

            receipt.ReceiptDate = dto.ReceiptDate;
            receipt.PaymentMethodId = dto.PaymentMethodId;
            receipt.PaymentMethod = paymentMethodName;
            receipt.BankId = isCash ? null : (dto.BankId > 0 ? dto.BankId : null);
            receipt.TransactionNo = isCash
                ? string.Empty
                : (string.IsNullOrWhiteSpace(dto.TransactionNo) ? string.Empty : dto.TransactionNo.Trim());
            order.UpdatedAt = KsaTime.Now;

            await _context.SaveChangesAsync(cancellationToken);
            return (await GetOrderAsync(orderId, cancellationToken))!;
        }

        public async Task CancelTransferredOrdersForRemovedExtrasAsync(
            IReadOnlyList<string> orderNos,
            int reservationRouteId,
            CancellationToken cancellationToken = default)
        {
            if (orderNos == null || orderNos.Count == 0)
            {
                return;
            }

            var hotel = await GetCurrentHotelSettingsAsync(cancellationToken);
            var hotelScopeIds = BuildReservationHotelScopeIds(hotel);

            var reservation = await PmsReservationRouteResolver.FindAsync(
                _context,
                reservationRouteId,
                hotelId: null,
                asNoTracking: false,
                cancellationToken);
            if (reservation == null)
            {
                return;
            }

            var reservationKeys = ReservationFinancialSyncService.BuildReservationKeys(
                reservation.ReservationId,
                reservation.ZaaerId);

            var now = KsaTime.Now;
            var changed = false;

            foreach (var orderNo in orderNos.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var order = await _context.Orders
                    .FirstOrDefaultAsync(
                        o =>
                            o.OrderNo == orderNo
                            && hotelScopeIds.Contains(o.HotelId)
                            && o.ReservationId != null
                            && reservationKeys.Contains(o.ReservationId.Value)
                            && o.OrderStatus.Equals(
                                "transferred_to_reservation",
                                StringComparison.OrdinalIgnoreCase),
                        cancellationToken);

                if (order == null
                    || order.OrderStatus.Equals("cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                order.OrderStatus = "cancelled";
                order.PaymentStatus = "cancelled";
                order.CancellationDate = now;
                order.CancellationReason = "حذف من إضافات الحجز";
                order.IsRefunded = false;
                order.UpdatedAt = now;
                order.Balance = 0;
                changed = true;
            }

            if (changed)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<PmsPosOrderDto> UpdateTransferredOrderAsync(
            int orderId,
            PmsUpdateTransferredPosOrderDto dto,
            CancellationToken cancellationToken = default)
        {
            if (dto.Lines == null || dto.Lines.Count == 0)
            {
                throw new ArgumentException("At least one order line is required.");
            }

            if (dto.DiscountAmount > 0 && !_currentUser.HasPermission("pos.orders.discount"))
            {
                throw new UnauthorizedAccessException("You do not have permission to apply a POS order discount.");
            }

            var hotel = await GetCurrentHotelSettingsAsync(cancellationToken);
            var hotelScopeIds = BuildReservationHotelScopeIds(hotel);
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderId == orderId && hotelScopeIds.Contains(o.HotelId), cancellationToken)
                ?? throw new ArgumentException("Order not found.");

            if (!order.OrderStatus.Equals("transferred_to_reservation", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Only transferred reservation orders can be edited here.");
            }

            if (order.ReservationId is not > 0)
            {
                throw new ArgumentException("Order is not linked to a reservation.");
            }

            var linkedReservation = await PmsReservationRouteResolver.FindAsync(
                _context,
                order.ReservationId.Value,
                hotelId: null,
                asNoTracking: false,
                cancellationToken)
                ?? throw new ArgumentException("Reservation not found.");

            if (!IsCheckedInReservationStatus(linkedReservation.Status))
            {
                throw new ArgumentException("pos.orders.reservationCheckedOut");
            }

            var lineGross = BuildLineGrossAmounts(dto.Lines).ToList();
            var grossSum = Math.Round(lineGross.Sum(), 2, MidpointRounding.AwayFromZero);
            if (dto.DiscountAmount > grossSum)
            {
                throw new ArgumentException("Discount cannot exceed the order total.");
            }

            var taxConfig = await ResolvePosTaxConfigAsync(hotel, cancellationToken);
            var (subtotal, tax, total) = HotelPricingTaxHelper.ComputePosOrderTotals(
                lineGross,
                dto.DiscountAmount,
                taxConfig);

            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = KsaTime.Now;
                var orderLinkId = ResolveOrderReceiptLinkId(order);
                var oldItems = await _context.OrderItems
                    .Where(i => i.OrderId == orderLinkId || i.OrderId == order.OrderId)
                    .ToListAsync(cancellationToken);
                if (oldItems.Count > 0)
                {
                    _context.OrderItems.RemoveRange(oldItems);
                }

                foreach (var line in dto.Lines)
                {
                    var qtyDec = line.Quantity <= 0 ? 1 : line.Quantity;
                    var qtyInt = Math.Max(1, (int)Math.Ceiling(qtyDec));
                    var (unitNet, lineGrossTotal) = SplitPosLineAmounts(
                        line.UnitPrice,
                        qtyDec,
                        line.Discount,
                        taxConfig);
                    await _context.OrderItems.AddAsync(new OrderItem
                    {
                        OrderId = orderLinkId,
                        ItemId = line.ItemId,
                        ItemName = line.ItemName.Trim(),
                        Quantity = qtyInt,
                        UnitPrice = unitNet,
                        Discount = line.Discount,
                        TotalPrice = lineGrossTotal,
                        CreatedAt = now
                    }, cancellationToken);
                }

                order.Subtotal = subtotal;
                order.TaxAmount = tax;
                order.DiscountAmount = dto.DiscountAmount;
                order.TotalAmount = total;
                order.PaidAmount = 0;
                order.Balance = 0;
                order.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? order.Notes : dto.Notes.Trim();
                order.UpdatedAt = now;

                await SyncPosTransferExtraAmountsAsync(
                    linkedReservation,
                    order,
                    subtotal,
                    tax,
                    total,
                    cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                return (await GetOrderAsync(order.OrderId, cancellationToken))!;
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        }

        private async Task SyncPosTransferExtraAmountsAsync(
            Reservation reservation,
            Order order,
            decimal subtotal,
            decimal taxAmount,
            decimal totalAmount,
            CancellationToken cancellationToken)
        {
            var keys = ReservationFinancialSyncService.BuildReservationKeys(
                reservation.ReservationId,
                reservation.ZaaerId);

            var extras = await _context.ReservationExtras
                .Where(e => keys.Contains(e.ReservationId))
                .ToListAsync(cancellationToken);

            var extra = extras.FirstOrDefault(e =>
                PosReservationExtraNaming.TryParseOrderNo(e.ItemName, out var no)
                && no.Equals(order.OrderNo, StringComparison.OrdinalIgnoreCase));

            if (extra == null)
            {
                throw new ArgumentException("Reservation extra line not found for this POS order.");
            }

            var grossTotal = Math.Round(totalAmount, 2, MidpointRounding.AwayFromZero);
            var netSubtotal = Math.Round(subtotal, 2, MidpointRounding.AwayFromZero);
            var tax = Math.Round(taxAmount, 2, MidpointRounding.AwayFromZero);

            extra.UnitPrice = grossTotal;
            extra.Subtotal = netSubtotal;
            extra.TaxAmount = tax;
            extra.TotalAmount = grossTotal;
            extra.UpdatedAt = KsaTime.Now;

            await _context.SaveChangesAsync(cancellationToken);
            await RecalculateReservationTotalsAfterPosExtraAsync(reservation.ReservationId, cancellationToken);
        }

        public async Task<PmsPosOrderDto> CancelOrderAsync(int orderId, CancellationToken cancellationToken = default)
        {
            var hotel = await GetCurrentHotelSettingsAsync(cancellationToken);
            var hotelId = hotel.HotelId;
            var hotelScopeIds = BuildReservationHotelScopeIds(hotel);
            var integrationHotelId = ResolveIntegrationHotelId(hotel);
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderId == orderId && hotelScopeIds.Contains(o.HotelId), cancellationToken)
                ?? throw new ArgumentException("Order not found.");

            if (order.OrderStatus.Equals("cancelled", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Order is already cancelled.");
            }

            var isTransferred = order.OrderStatus.Equals(
                "transferred_to_reservation",
                StringComparison.OrdinalIgnoreCase);
            var primaryReceipt = isTransferred ? null : await FindPrimaryPosReceiptAsync(orderId, cancellationToken);

            Reservation? linkedReservation = null;
            if (order.ReservationId is > 0)
            {
                linkedReservation = await PmsReservationRouteResolver.FindAsync(
                    _context,
                    order.ReservationId.Value,
                    hotelId: null,
                    asNoTracking: false,
                    cancellationToken);
            }

            if (isTransferred)
            {
                if (linkedReservation == null)
                {
                    throw new ArgumentException("Reservation not found for this transferred order.");
                }

                if (!IsCheckedInReservationStatus(linkedReservation.Status))
                {
                    throw new ArgumentException("pos.orders.reservationCheckedOut");
                }
            }

            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = KsaTime.Now;
                order.OrderStatus = "cancelled";
                order.PaymentStatus = "cancelled";
                order.CancellationDate = now;
                order.CancellationReason = "إلغاء الطلب";
                order.IsRefunded = true;
                order.UpdatedAt = now;
                order.Balance = 0;

                if (primaryReceipt != null && order.TotalAmount.HasValue && order.TotalAmount.Value > 0)
                {
                    var refundIdentity = await _numberingService.GetNextBusinessIdentityAsync(
                        "payment_refund",
                        hotelId,
                        PmsCurrentUser.ResolveDisplayName(_currentUser),
                        $"pms-pos-refund:{hotelId}:{orderId}:{Guid.NewGuid():N}",
                        cancellationToken);

                    var refundAmount = -Math.Round(order.TotalAmount.Value, 2, MidpointRounding.AwayFromZero);
                    var refund = new PaymentReceipt
                    {
                        ReceiptNo = refundIdentity.DocumentNo,
                        ZaaerId = ZaaerIdMapper.ToNullableInt32(refundIdentity.ZaaerId),
                        HotelId = integrationHotelId,
                        OrderId = ResolveOrderReceiptLinkId(order),
                        ReservationId = order.ReservationId,
                        CustomerId = order.CustomerId,
                        ReceiptDate = now,
                        ReceiptType = "refund",
                        VoucherCode = "refund",
                        AmountPaid = refundAmount,
                        PaymentMethodId = primaryReceipt.PaymentMethodId,
                        PaymentMethod = primaryReceipt.PaymentMethod,
                        TransactionNo = string.Empty,
                        Notes = $"Refund for POS order {order.OrderNo}",
                        Reason = "إلغاء الطلب",
                        ReceiptStatus = "paid",
                        CreatedBy = PmsCurrentUser.ResolveUserId(_currentUser),
                        CreatedAt = now
                    };

                    await _context.PaymentReceipts.AddAsync(refund, cancellationToken);
                    await _numberingService.MarkCommittedAsync(refundIdentity.AuditId, cancellationToken);
                }

                if (linkedReservation != null && isTransferred)
                {
                    await RemovePosTransferExtraAsync(linkedReservation, order, cancellationToken);
                }

                await _context.SaveChangesAsync(cancellationToken);

                await tx.CommitAsync(cancellationToken);

                return (await GetOrderAsync(orderId, cancellationToken))!;
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        }

        private static PmsPosOrderDto MapOrder(Order order, IReadOnlyList<PmsPosOrderLineDto> lines, PaymentReceipt? receipt) =>
            new()
            {
                OrderId = order.OrderId,
                OrderNo = order.OrderNo,
                HotelId = order.HotelId,
                ReservationId = order.ReservationId,
                OutletId = order.OutletId,
                OutletName = order.Outlet?.OutletName,
                OrderStatus = order.OrderStatus,
                PaymentStatus = order.PaymentStatus,
                OrderType = order.OrderType,
                Subtotal = order.Subtotal,
                TaxAmount = order.TaxAmount,
                DiscountAmount = order.DiscountAmount,
                TotalAmount = order.TotalAmount,
                PaidAmount = order.PaidAmount,
                Balance = order.Balance,
                OrderDate = order.OrderDate,
                OrderTime = order.OrderTime,
                CreatedAt = order.CreatedAt,
                CreatedBy = order.CreatedBy,
                ReceiptId = receipt?.ReceiptId,
                ReceiptNo = receipt?.ReceiptNo,
                PaymentMethodId = receipt?.PaymentMethodId,
                PaymentMethod = receipt?.PaymentMethod,
                Lines = lines
            };
    }
}
