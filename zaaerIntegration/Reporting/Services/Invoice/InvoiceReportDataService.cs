using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.Reporting.Abstractions;
using zaaerIntegration.Reporting.Base;
using zaaerIntegration.Reporting.DTOs.Invoice;
using zaaerIntegration.Reporting.DTOs.Shared;
using zaaerIntegration.Reporting.Utilities;
using zaaerIntegration.Security;
using zaaerIntegration.Services.Integrations.Zatca;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Reporting.Services.Invoice;

public sealed class InvoiceReportDataService : PmsReportDataServiceBase, IInvoiceReportDataService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserContext _currentUser;

    public InvoiceReportDataService(
        ApplicationDbContext context,
        ITenantService tenantService,
        IReportAssetCache assetCache,
        ICurrentUserContext currentUser)
        : base(context, tenantService, assetCache)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<InvoiceReportDto> GetByZaaerIdAsync(int invoiceZaaerId, CancellationToken cancellationToken = default)
    {
        var hotelIds = await ResolveScopedHotelIdsAsync(cancellationToken);

        var invoice = await _context.Invoices.AsNoTracking()
            .Where(i => i.ZaaerId == invoiceZaaerId && hotelIds.Contains(i.HotelId))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Invoice zaaer_id {invoiceZaaerId} was not found.");

        return await BuildReportDtoAsync(invoice, cancellationToken);
    }

    public async Task<InvoiceReportDto> GetAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        var hotelIds = await ResolveScopedHotelIdsAsync(cancellationToken);

        var invoice = await _context.Invoices.AsNoTracking()
            .Where(i => i.InvoiceId == invoiceId && hotelIds.Contains(i.HotelId))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Invoice {invoiceId} was not found.");

        return await BuildReportDtoAsync(invoice, cancellationToken);
    }

    private async Task<HashSet<int>> ResolveScopedHotelIdsAsync(CancellationToken cancellationToken)
    {
        var hotel = await GetCurrentHotelSettingsAsync(cancellationToken);
        var hotelZaaerId = hotel.ZaaerId ?? hotel.HotelId;
        return new HashSet<int> { hotel.HotelId, hotelZaaerId };
    }

    private async Task<InvoiceReportDto> BuildReportDtoAsync(
        FinanceLedgerAPI.Models.Invoice invoice,
        CancellationToken cancellationToken)
    {
        // EF DbContext is not thread-safe — keep queries sequential on the scoped context.
        var header = await BuildHotelHeaderAsync(cancellationToken);
        var customer = await LoadCustomerAsync(invoice.CustomerId, cancellationToken);
        var reservationCtx = await LoadReservationContextAsync(invoice, cancellationToken);
        var operatorInfo = await LoadOperatorSignatureAsync(cancellationToken);
        var stay = await LoadStayAsync(invoice, reservationCtx.Reservation, cancellationToken);
        var receipts = await LoadPaymentReceiptsAsync(invoice, cancellationToken);
        var lines = BuildSyntheticLines(invoice, stay);
        var zatcaQr = ZatcaQrPayloadResolver.Resolve(invoice.ZatcaQr);
        var generatedAt = ReportGeneratedAt();
        var hijriDate = HijriDateHelper.ResolveInvoiceHijri(invoice.InvoiceDate, invoice.InvoiceDateHijri);
        var amountWords = AmountInWordsHelper.FormatSar(invoice.TotalAmount ?? 0m);

        var subtotal = invoice.Subtotal ?? 0m;
        var vatAmount = invoice.VatAmount ?? 0m;
        var total = invoice.TotalAmount ?? 0m;
        var amountPaid = invoice.AmountPaid;
        var amountRemaining = invoice.AmountRemaining ?? Math.Max(0m, total - amountPaid);
        var isStandard = ResolveIsStandardInvoice(invoice.ZatcaProfile, reservationCtx.Corporate);
        var zatcaProfile = isStandard ? "standard" : "simplified";

        return new InvoiceReportDto
        {
            ReportVersion = ReportVersions.Invoice_v1,
            Header = header,
            Invoice = new InvoiceReportHeaderDto
            {
                InvoiceId = invoice.InvoiceId,
                ZaaerId = invoice.ZaaerId,
                InvoiceNo = invoice.InvoiceNo,
                InvoiceDate = invoice.InvoiceDate,
                InvoiceDateHijri = hijriDate,
                PeriodFrom = invoice.PeriodFrom,
                PeriodTo = invoice.PeriodTo,
                InvoiceType = invoice.InvoiceType ?? "rent",
                PaymentStatus = invoice.PaymentStatus ?? "unpaid",
                Notes = invoice.Notes,
                ReservationNo = stay?.ReservationNo
            },
            Customer = customer,
            Corporate = reservationCtx.Corporate,
            ZatcaProfile = zatcaProfile,
            IsStandardInvoice = isStandard,
            Stay = stay,
            InvoiceTitleAr = isStandard ? "فاتورة ضريبية" : "فاتورة ضريبية مبسطة",
            InvoiceTitleEn = isStandard ? "TAX INVOICE" : "SIMPLIFIED TAX INVOICE",
            Lines = lines,
            Tax = new InvoiceReportTaxSummaryDto
            {
                Subtotal = subtotal,
                VatRate = invoice.VatRate ?? 0m,
                VatAmount = vatAmount,
                LodgingTaxRate = invoice.LodgingTaxRate ?? 0m,
                LodgingTaxAmount = invoice.LodgingTaxAmount ?? 0m,
                TotalAmount = total,
                AmountPaid = amountPaid,
                AmountRemaining = amountRemaining
            },
            Payments = new InvoiceReportPaymentSummaryDto
            {
                Receipts = receipts,
                TotalPaid = receipts.Sum(r => r.AmountPaid)
            },
            QrImageBytes = zatcaQr.ImageBytes,
            ZatcaQrTlvBytes = zatcaQr.TlvBytes,
            ZatcaStatus = invoice.ZatcaStatus,
            ShowZatcaQr = zatcaQr.HasPayload,
            Footer = new ReportFooterDto
            {
                FooterText = "Zaaer PMS — Tax Invoice",
                GeneratedAt = generatedAt
            },
            GeneratedAt = generatedAt,
            TotalAmountWordsAr = amountWords.Arabic,
            TotalAmountWordsEn = amountWords.English,
            OperatorDisplayName = operatorInfo.DisplayName,
            OperatorSignatureBytes = operatorInfo.SignatureBytes
        };
    }

    private async Task<OperatorReportInfo> LoadOperatorSignatureAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await LoadOperatorSignatureCoreAsync(cancellationToken);
        }
        catch
        {
            return new OperatorReportInfo
            {
                DisplayName = PmsCurrentUser.ResolveDisplayName(_currentUser)
            };
        }
    }

    private async Task<OperatorReportInfo> LoadOperatorSignatureCoreAsync(CancellationToken cancellationToken)
    {
        var userId = PmsCurrentUser.ResolveUserId(_currentUser);
        if (userId is not > 0)
        {
            return OperatorReportInfo.Empty;
        }

        var tenant = TenantServiceRef.GetTenant();
        var hotelCode = tenant?.Code?.Trim() ?? "default";

        var user = await _context.Users.AsNoTracking()
            .Where(u => u.ZaaerId == userId || u.UserId == userId)
            .Select(u => new
            {
                u.FirstName,
                u.LastName,
                u.SignatureUrl
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            return new OperatorReportInfo
            {
                DisplayName = PmsCurrentUser.ResolveDisplayName(_currentUser)
            };
        }

        var displayName = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = PmsCurrentUser.ResolveDisplayName(_currentUser);
        }

        byte[]? signatureBytes = null;
        if (!string.IsNullOrWhiteSpace(user.SignatureUrl))
        {
            // Signature image is optional; never block invoice PDF on slow/missing asset URLs.
            try
            {
                signatureBytes = await AssetCache.GetLogoBytesAsync(
                    hotelCode,
                    user.SignatureUrl,
                    cancellationToken);
            }
            catch
            {
                signatureBytes = null;
            }
        }

        return new OperatorReportInfo
        {
            DisplayName = displayName,
            SignatureBytes = signatureBytes
        };
    }

    private sealed class OperatorReportInfo
    {
        public static OperatorReportInfo Empty { get; } = new();

        public string? DisplayName { get; init; }
        public byte[]? SignatureBytes { get; init; }
    }

    private async Task<InvoiceReportCustomerDto?> LoadCustomerAsync(
        int? customerId,
        CancellationToken cancellationToken)
    {
        if (!customerId.HasValue)
        {
            return null;
        }

        return await _context.Customers.AsNoTracking()
            .Where(c => c.CustomerId == customerId.Value || c.ZaaerId == customerId.Value)
            .Select(c => new InvoiceReportCustomerDto
            {
                CustomerId = c.CustomerId,
                ZaaerId = c.ZaaerId,
                CustomerName = c.CustomerName,
                MobileNo = c.MobileNo,
                Email = c.Email,
                Address = c.Address,
                CustomerNo = c.CustomerNo
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<ReservationContextResult> LoadReservationContextAsync(
        FinanceLedgerAPI.Models.Invoice invoice,
        CancellationToken cancellationToken)
    {
        var reservationRef = invoice.ReservationId;
        if (reservationRef is not > 0)
        {
            return ReservationContextResult.Empty;
        }

        var reservation = await ZatcaReservationLinkage.FindReservationAsync(
            _context,
            reservationRef.Value,
            invoice.HotelId,
            cancellationToken);

        if (reservation == null)
        {
            return ReservationContextResult.Empty;
        }

        FinanceLedgerAPI.Models.CorporateCustomer? corporateEntity = null;
        if (reservation.CorporateId is > 0)
        {
            corporateEntity = await ZatcaReservationLinkage.FindCorporateCustomerAsync(
                _context,
                invoice.HotelId,
                reservation.CorporateId.Value,
                cancellationToken);
        }

        InvoiceReportCorporateDto? corporate = corporateEntity != null ? MapCorporate(corporateEntity) : null;

        return new ReservationContextResult
        {
            Reservation = reservation,
            Corporate = corporate
        };
    }

    private static InvoiceReportCorporateDto MapCorporate(FinanceLedgerAPI.Models.CorporateCustomer corporateEntity) =>
        new()
        {
            CorporateId = corporateEntity.CorporateId,
            CompanyName = !string.IsNullOrWhiteSpace(corporateEntity.CorporateNameAr)
                ? corporateEntity.CorporateNameAr
                : corporateEntity.CorporateName,
            TaxNumber = corporateEntity.VatRegistrationNo,
            CrNumber = corporateEntity.CommercialRegistrationNo ?? corporateEntity.CorNo,
            Address = !string.IsNullOrWhiteSpace(corporateEntity.AddressAr)
                ? corporateEntity.AddressAr
                : corporateEntity.Address,
            City = !string.IsNullOrWhiteSpace(corporateEntity.CityAr)
                ? corporateEntity.CityAr
                : corporateEntity.City,
            Phone = corporateEntity.CorporatePhone,
            Email = corporateEntity.Email
        };

    private async Task<InvoiceReportStayDto?> LoadStayAsync(
        FinanceLedgerAPI.Models.Invoice invoice,
        FinanceLedgerAPI.Models.Reservation? reservation,
        CancellationToken cancellationToken)
    {
        if (reservation == null)
        {
            return null;
        }

        var checkIn = invoice.PeriodFrom ?? reservation.CheckInDate;
        var checkOut = invoice.PeriodTo ?? reservation.CheckOutDate ?? reservation.DepartureDate;
        var nights = reservation.TotalNights;
        if (!nights.HasValue && checkIn.HasValue && checkOut.HasValue)
        {
            nights = Math.Max(1, (checkOut.Value.Date - checkIn.Value.Date).Days);
        }

        var unitsText = await LoadUnitsTextAsync(reservation, cancellationToken);
        var periodText = checkIn.HasValue && checkOut.HasValue
            ? $"{checkIn:dd/MM/yyyy} - {checkOut:dd/MM/yyyy}"
            : null;

        return new InvoiceReportStayDto
        {
            ReservationNo = reservation.ReservationNo,
            CheckInDate = checkIn,
            CheckOutDate = checkOut,
            Nights = nights,
            UnitsText = unitsText,
            PeriodText = periodText
        };
    }

    private async Task<string?> LoadUnitsTextAsync(
        FinanceLedgerAPI.Models.Reservation reservation,
        CancellationToken cancellationToken)
    {
        var reservationKeys = new List<int> { reservation.ReservationId };
        if (reservation.ZaaerId is > 0)
        {
            reservationKeys.Add(reservation.ZaaerId.Value);
        }

        var units = await _context.ReservationUnits.AsNoTracking()
            .Where(ru => reservationKeys.Contains(ru.ReservationId))
            .ToListAsync(cancellationToken);

        if (units.Count == 0)
        {
            return null;
        }

        var apartmentKeys = units.Select(u => u.ApartmentId).Distinct().ToList();
        var apartments = await _context.Apartments.AsNoTracking()
            .Where(a => apartmentKeys.Contains(a.ApartmentId) || (a.ZaaerId.HasValue && apartmentKeys.Contains(a.ZaaerId.Value)))
            .ToListAsync(cancellationToken);

        var roomTypeIds = apartments
            .Where(a => a.RoomTypeId.HasValue)
            .Select(a => a.RoomTypeId!.Value)
            .Distinct()
            .ToList();

        var roomTypes = roomTypeIds.Count == 0
            ? new Dictionary<int, string>()
            : await _context.RoomTypes.AsNoTracking()
                .Where(rt => roomTypeIds.Contains(rt.RoomTypeId))
                .ToDictionaryAsync(rt => rt.RoomTypeId, rt => rt.RoomTypeName, cancellationToken);

        var labels = new List<string>();
        foreach (var unit in units)
        {
            var apt = apartments.FirstOrDefault(a =>
                a.ApartmentId == unit.ApartmentId || a.ZaaerId == unit.ApartmentId);
            if (apt == null)
            {
                continue;
            }

            var typeName = apt.RoomTypeId.HasValue && roomTypes.TryGetValue(apt.RoomTypeId.Value, out var tn)
                ? tn
                : apt.ApartmentName;
            var code = !string.IsNullOrWhiteSpace(apt.ApartmentCode) ? apt.ApartmentCode : apt.ApartmentName;
            labels.Add($"{typeName} - {code}");
        }

        return labels.Count == 0 ? null : string.Join(" ، ", labels);
    }

    private static bool ResolveIsStandardInvoice(
        string? zatcaProfile,
        InvoiceReportCorporateDto? corporate)
    {
        if (string.Equals(zatcaProfile?.Trim(), "standard", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // If the reservation has a corporate buyer, the printable document must be a
        // standard Tax Invoice even when old rows still carry simplified/blank profile.
        return corporate != null
               && (
                   !string.IsNullOrWhiteSpace(corporate.CompanyName)
                   || !string.IsNullOrWhiteSpace(corporate.TaxNumber)
                   || !string.IsNullOrWhiteSpace(corporate.CrNumber)
               );
    }

    private static bool IsStandardZatcaProfile(string? zatcaProfile) =>
        ResolveIsStandardInvoice(zatcaProfile, null);

    private static string NormalizeZatcaProfile(string? zatcaProfile) =>
        IsStandardZatcaProfile(zatcaProfile) ? "standard" : "simplified";

    private sealed class ReservationContextResult
    {
        public static ReservationContextResult Empty { get; } = new();

        public FinanceLedgerAPI.Models.Reservation? Reservation { get; init; }
        public InvoiceReportCorporateDto? Corporate { get; init; }
    }

    private static IReadOnlyList<InvoiceReportLineDto> BuildSyntheticLines(
        FinanceLedgerAPI.Models.Invoice invoice,
        InvoiceReportStayDto? stay)
    {
        var subtotal = invoice.Subtotal ?? invoice.TotalAmount ?? 0m;
        var vat = invoice.VatAmount ?? 0m;
        var vatRate = invoice.VatRate ?? 0m;
        var lodging = invoice.LodgingTaxAmount ?? 0m;
        var lodgingRate = invoice.LodgingTaxRate ?? 0m;
        var total = invoice.TotalAmount ?? subtotal + lodging + vat;
        var qty = stay?.Nights is > 0 ? stay.Nights.Value : 1m;
        var unitPrice = qty > 0 ? subtotal / qty : subtotal;
        var description = BuildLineDescription(invoice, stay);

        return new[]
        {
            new InvoiceReportLineDto
            {
                LineNumber = 1,
                Description = description,
                Quantity = qty,
                UnitPrice = unitPrice,
                LineTotal = subtotal,
                VatRate = vatRate,
                VatAmount = vat,
                LodgingTaxRate = lodgingRate,
                LodgingTaxAmount = lodging,
                TotalWithVat = total,
                PriceAfterDiscount = unitPrice
            }
        };
    }

    private static string BuildLineDescription(
        FinanceLedgerAPI.Models.Invoice invoice,
        InvoiceReportStayDto? stay)
    {
        // Enterprise-grade rich description matching the exact format in the tax invoice images
        var typeLabel = invoice.InvoiceType switch
        {
            "rent" => "مجموع الحجز",
            "service" => "مجموع الخدمات",
            "pos" => "مجموع المبيعات",
            _ => "مجموع الفاتورة"
        };

        if (stay != null && !string.IsNullOrWhiteSpace(stay.UnitsText))
        {
            // Example output: "مجموع الحجز - غرفة (حمام) - 407 - 09/06/2026 - 10/06/2026"
            var unitPart = stay.UnitsText.Trim();
            var periodPart = !string.IsNullOrWhiteSpace(stay.PeriodText)
                ? stay.PeriodText
                : BuildPeriod(invoice);

            var desc = $"{typeLabel} - {unitPart}";
            if (!string.IsNullOrWhiteSpace(periodPart))
            {
                desc += $"\n{periodPart}";
            }
            return desc;
        }

        return BuildLineDescriptionFallback(invoice);
    }

    private static string BuildLineDescriptionFallback(FinanceLedgerAPI.Models.Invoice invoice)
    {
        var typeLabel = invoice.InvoiceType switch
        {
            "rent" => "مجموع الحجز",
            "service" => "مجموع الخدمات",
            "pos" => "مجموع المبيعات",
            _ => "مجموع الفاتورة"
        };

        if (invoice.PeriodFrom.HasValue && invoice.PeriodTo.HasValue)
        {
            return $"{typeLabel}\nمن {invoice.PeriodFrom:dd/MM/yyyy} إلى {invoice.PeriodTo:dd/MM/yyyy}";
        }

        if (!string.IsNullOrWhiteSpace(invoice.Notes))
        {
            return $"{typeLabel}\n{invoice.Notes}";
        }

        return typeLabel;
    }

    private async Task<IReadOnlyList<InvoiceReportPaymentRowDto>> LoadPaymentReceiptsAsync(
        FinanceLedgerAPI.Models.Invoice invoice,
        CancellationToken cancellationToken)
    {
        var invoiceId = invoice.InvoiceId;
        var mappingInvoiceIds = new List<int> { invoiceId };
        if (invoice.ZaaerId.HasValue && invoice.ZaaerId.Value != invoiceId)
        {
            mappingInvoiceIds.Add(invoice.ZaaerId.Value);
        }

        var receiptIdsFromMappings = await _context.InvoiceReceiptMappings.AsNoTracking()
            .Where(m => mappingInvoiceIds.Contains(m.InvoiceId))
            .Select(m => m.ReceiptId)
            .ToListAsync(cancellationToken);

        var receiptsFromInvoiceId = await _context.PaymentReceipts.AsNoTracking()
            .Where(pr => pr.InvoiceId == invoiceId)
            .ToListAsync(cancellationToken);

        var receiptsFromMappings = receiptIdsFromMappings.Count == 0
            ? new List<FinanceLedgerAPI.Models.PaymentReceipt>()
            : await _context.PaymentReceipts.AsNoTracking()
                .Where(pr => receiptIdsFromMappings.Contains(pr.ReceiptId))
                .ToListAsync(cancellationToken);

        return receiptsFromInvoiceId
            .Concat(receiptsFromMappings)
            .GroupBy(r => r.ReceiptId)
            .Select(g => g.First())
            .OrderBy(r => r.ReceiptDate)
            .Select(r => new InvoiceReportPaymentRowDto
            {
                ReceiptNo = r.ReceiptNo,
                ReceiptDate = r.ReceiptDate,
                AmountPaid = r.AmountPaid,
                PaymentMethod = r.PaymentMethod
            })
            .ToList();
    }

    private static string BuildPeriod(FinanceLedgerAPI.Models.Invoice invoice)
    {
        if (invoice.PeriodFrom.HasValue && invoice.PeriodTo.HasValue)
        {
            return $"من {invoice.PeriodFrom:dd/MM/yyyy} إلى {invoice.PeriodTo:dd/MM/yyyy}";
        }

        return string.Empty;
    }

}
