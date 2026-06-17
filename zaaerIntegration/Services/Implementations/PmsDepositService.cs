#pragma warning disable CS1591

using FinanceLedgerAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Security;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    /// <summary>
    /// PMS bank deposits stored as <c>payment_receipts</c> rows (<c>voucher_code = transfers_to_bank</c>).
    /// </summary>
    public sealed class PmsDepositService : IPmsDepositService
    {
        private const string VoucherCode = "transfers_to_bank";
        private const string ReceiptType = "refund";

        private readonly ApplicationDbContext _context;
        private readonly ITenantService _tenantService;
        private readonly INumberingService _numberingService;
        private readonly ICurrentUserContext _currentUser;
        private readonly IDepositImageService _depositImageService;

        public PmsDepositService(
            ApplicationDbContext context,
            ITenantService tenantService,
            INumberingService numberingService,
            ICurrentUserContext currentUser,
            IDepositImageService depositImageService)
        {
            _context = context;
            _tenantService = tenantService;
            _numberingService = numberingService;
            _currentUser = currentUser;
            _depositImageService = depositImageService;
        }

        public async Task<PmsDepositListResultDto> ListAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken cancellationToken = default)
        {
            var hotel = await ResolveHotelContextAsync(cancellationToken);
            var banks = await LoadBankMapAsync(cancellationToken);

            var query = BaseDepositQuery(hotel);

            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                query = query.Where(r => r.ReceiptDate >= from);
            }

            if (toDate.HasValue)
            {
                var to = toDate.Value.Date.AddDays(1);
                query = query.Where(r => r.ReceiptDate < to);
            }

            var rows = await query
                .OrderByDescending(r => r.ReceiptDate)
                .ThenByDescending(r => r.ReceiptId)
                .ToListAsync(cancellationToken);

            var receiptZaaerIds = rows
                .Where(r => r.ZaaerId.HasValue && r.ZaaerId.Value > 0)
                .Select(r => r.ZaaerId!.Value)
                .ToList();
            var imageMap = await LoadFirstImageMapAsync(receiptZaaerIds, cancellationToken);

            var items = rows
                .Select(r => MapRow(r, banks, imageMap))
                .ToList();

            return new PmsDepositListResultDto
            {
                Items = items,
                Summary = BuildSummary(items)
            };
        }

        public async Task<PmsDepositDetailDto?> GetByIdAsync(int receiptId, CancellationToken cancellationToken = default)
        {
            var hotel = await ResolveHotelContextAsync(cancellationToken);
            var banks = await LoadBankMapAsync(cancellationToken);

            var row = await BaseDepositQuery(hotel)
                .FirstOrDefaultAsync(r => r.ReceiptId == receiptId, cancellationToken);

            if (row == null)
            {
                return null;
            }

            var imageMap = row.ZaaerId is > 0
                ? await LoadFirstImageMapAsync(new[] { row.ZaaerId.Value }, cancellationToken)
                : new Dictionary<int, (string? ImagePath, int Count)>();
            var detail = MapDetail(row, banks, imageMap);
            detail.Images = (await _depositImageService.GetImagesAsync(receiptId, cancellationToken)).ToList();
            return detail;
        }

        public async Task<PmsDepositDetailDto> CreateAsync(PmsCreateDepositDto dto, CancellationToken cancellationToken = default)
        {
            var hotel = await ResolveHotelContextAsync(cancellationToken);
            var bank = await ResolveBankAsync(dto.BankZaaerId, cancellationToken);
            var paymentMethod = await ResolvePaymentMethodAsync(dto.PaymentMethodId, cancellationToken);
            var bankSlug = RequireBankSlug(bank);

            var pmsUserId = PmsCurrentUser.ResolveUserId(_currentUser);
            var identity = await _numberingService.GetNextBusinessIdentityAsync(
                "payment_refund",
                hotel.HotelZaaerId,
                (pmsUserId?.ToString() ?? "pms"),
                $"pms-deposit:{hotel.HotelZaaerId}:{Guid.NewGuid():N}",
                cancellationToken);

            var amount = -Math.Abs(dto.AmountPaid);

            var receipt = new PaymentReceipt
            {
                ReceiptNo = identity.DocumentNo,
                ZaaerId = ZaaerIdMapper.ToNullableInt32(identity.ZaaerId),
                HotelId = hotel.HotelZaaerId,
                ReservationId = null,
                CustomerId = null,
                ReceiptDate = KsaTime.CombineDateWithCurrentTime(dto.ReceiptDate),
                ReceiptType = ReceiptType,
                VoucherCode = VoucherCode,
                AmountPaid = amount,
                PaymentMethodId = paymentMethod.PaymentMethodId,
                PaymentMethod = paymentMethod.MethodName,
                BankId = bank.ZaaerId,
                BankName = bankSlug,
                TransactionNo = string.Empty,
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? string.Empty : dto.Notes.Trim(),
                ReceiptStatus = "paid",
                CreatedBy = pmsUserId,
                CreatedAt = KsaTime.Now
            };

            _context.PaymentReceipts.Add(receipt);
            await _context.SaveChangesAsync(cancellationToken);

            return (await GetByIdAsync(receipt.ReceiptId, cancellationToken))!;
        }

        public async Task<PmsDepositDetailDto?> UpdateAsync(
            int receiptId,
            PmsUpdateDepositDto dto,
            CancellationToken cancellationToken = default)
        {
            var hotel = await ResolveHotelContextAsync(cancellationToken);
            var hotelIds = ResolveHotelIds(hotel);

            var receipt = await _context.PaymentReceipts
                .FirstOrDefaultAsync(
                    r => r.ReceiptId == receiptId
                         && hotelIds.Contains(r.HotelId)
                         && r.VoucherCode == VoucherCode,
                    cancellationToken);

            if (receipt == null || IsCancelled(receipt))
            {
                return null;
            }

            if (dto.ReceiptDate.HasValue)
            {
                receipt.ReceiptDate = KsaTime.CombineDateWithCurrentTime(dto.ReceiptDate.Value);
            }

            if (dto.AmountPaid.HasValue)
            {
                receipt.AmountPaid = -Math.Abs(dto.AmountPaid.Value);
            }

            if (dto.BankZaaerId.HasValue)
            {
                var bank = await ResolveBankAsync(dto.BankZaaerId.Value, cancellationToken);
                receipt.BankId = bank.ZaaerId;
                receipt.BankName = RequireBankSlug(bank);
            }

            if (dto.PaymentMethodId.HasValue)
            {
                var paymentMethod = await ResolvePaymentMethodAsync(dto.PaymentMethodId.Value, cancellationToken);
                receipt.PaymentMethodId = paymentMethod.PaymentMethodId;
                receipt.PaymentMethod = paymentMethod.MethodName;
            }

            if (dto.Notes != null)
            {
                receipt.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? string.Empty : dto.Notes.Trim();
            }

            await _context.SaveChangesAsync(cancellationToken);
            return await GetByIdAsync(receiptId, cancellationToken);
        }

        public async Task<bool> DeleteAsync(int receiptId, CancellationToken cancellationToken = default)
        {
            var hotel = await ResolveHotelContextAsync(cancellationToken);
            var hotelIds = ResolveHotelIds(hotel);

            var receipt = await _context.PaymentReceipts
                .FirstOrDefaultAsync(
                    r => r.ReceiptId == receiptId
                         && hotelIds.Contains(r.HotelId)
                         && r.VoucherCode == VoucherCode,
                    cancellationToken);

            if (receipt == null || IsCancelled(receipt))
            {
                return false;
            }

            receipt.ReceiptStatus = "cancelled";
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<IReadOnlyList<PmsDepositBankDto>> GetBanksAsync(CancellationToken cancellationToken = default)
        {
            var banks = await _context.Banks
                .AsNoTracking()
                .Where(b => b.IsActive && b.ZaaerId.HasValue && b.ZaaerId.Value > 0)
                .OrderByDescending(b => b.IsDefault)
                .ThenBy(b => b.SortOrder)
                .ThenBy(b => b.BankNameAr)
                .ToListAsync(cancellationToken);

            return banks
                .Select(b => new PmsDepositBankDto
                {
                    Id = b.ZaaerId!.Value,
                    BankId = b.BankId,
                    ZaaerId = b.ZaaerId,
                    Name = b.BankNameEn,
                    NameAr = b.BankNameAr,
                    BankSlug = DepositBankNameNormalizer.FromBank(b),
                    IsDefault = b.IsDefault
                })
                .ToList();
        }

        public async Task<IReadOnlyList<PmsDepositPaymentMethodDto>> GetPaymentMethodsAsync(
            CancellationToken cancellationToken = default)
        {
            return await _context.PaymentMethods
                .AsNoTracking()
                .Where(pm => pm.IsActive)
                .OrderBy(pm => pm.SortOrder)
                .ThenBy(pm => pm.MethodName)
                .Select(pm => new PmsDepositPaymentMethodDto
                {
                    Id = pm.PaymentMethodId,
                    Name = pm.MethodName,
                    NameAr = pm.MethodNameAr,
                    Code = pm.MethodCode
                })
                .ToListAsync(cancellationToken);
        }

        public Task<IReadOnlyList<PmsDepositImageDto>> GetImagesAsync(int receiptId, CancellationToken cancellationToken = default) =>
            _depositImageService.GetImagesAsync(receiptId, cancellationToken);

        public Task<IReadOnlyList<PmsDepositImageDto>> UploadImagesAsync(
            int receiptId,
            IReadOnlyList<IFormFile> images,
            CancellationToken cancellationToken = default) =>
            _depositImageService.UploadImagesAsync(receiptId, images, cancellationToken);

        public Task<bool> DeleteImageAsync(int receiptId, int imageId, CancellationToken cancellationToken = default) =>
            _depositImageService.DeleteImageAsync(receiptId, imageId, cancellationToken);

        private IQueryable<PaymentReceipt> BaseDepositQuery(HotelContext hotel)
        {
            var hotelIds = ResolveHotelIds(hotel);
            var bilad = DepositBankNameNormalizer.Bilad;
            var riyad = DepositBankNameNormalizer.Riyad;

            return _context.PaymentReceipts
                .AsNoTracking()
                .Where(r =>
                    hotelIds.Contains(r.HotelId)
                    && r.VoucherCode == VoucherCode
                    && r.BankName != null
                    && (r.BankName == bilad || r.BankName == riyad)
                    && (r.ReceiptStatus == null || r.ReceiptStatus != "cancelled"));
        }

        private sealed record HotelContext(int LocalHotelId, int HotelZaaerId);

        private static List<int> ResolveHotelIds(HotelContext hotel) =>
            new[] { hotel.LocalHotelId, hotel.HotelZaaerId }.Distinct().ToList();

        private async Task<HotelContext> ResolveHotelContextAsync(CancellationToken cancellationToken)
        {
            var tenant = _tenantService.GetTenant()
                ?? throw new UnauthorizedAccessException("Tenant not resolved. Provide X-Hotel-Code.");

            var hotelSettings = await _context.HotelSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    h => h.HotelCode != null && h.HotelCode.ToLower() == tenant.Code.ToLower(),
                    cancellationToken)
                ?? throw new InvalidOperationException($"HotelSettings not found for hotel code: {tenant.Code}.");

            if (!hotelSettings.ZaaerId.HasValue)
            {
                throw new InvalidOperationException($"ZaaerId is not configured for hotel code: {tenant.Code}.");
            }

            return new HotelContext(hotelSettings.HotelId, hotelSettings.ZaaerId.Value);
        }

        private async Task<int> ResolveHotelZaaerIdAsync(CancellationToken cancellationToken) =>
            (await ResolveHotelContextAsync(cancellationToken)).HotelZaaerId;

        private async Task<Bank> ResolveBankAsync(int bankZaaerId, CancellationToken cancellationToken)
        {
            var bank = await _context.Banks
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.ZaaerId == bankZaaerId && b.IsActive, cancellationToken)
                ?? throw new ArgumentException("Selected bank was not found.");

            if (RequireBankSlug(bank) == null)
            {
                throw new ArgumentException("Only Riyad Bank or Al Bilad Bank deposits are supported.");
            }

            return bank;
        }

        private static string RequireBankSlug(Bank bank)
        {
            var slug = DepositBankNameNormalizer.FromBank(bank);
            if (string.IsNullOrWhiteSpace(slug))
            {
                throw new ArgumentException("Only Riyad Bank or Al Bilad Bank deposits are supported.");
            }

            return slug;
        }

        private async Task<PaymentMethod> ResolvePaymentMethodAsync(int paymentMethodId, CancellationToken cancellationToken)
        {
            return await _context.PaymentMethods
                .AsNoTracking()
                .FirstOrDefaultAsync(pm => pm.PaymentMethodId == paymentMethodId && pm.IsActive, cancellationToken)
                ?? throw new ArgumentException("Selected payment method was not found.");
        }

        private async Task<Dictionary<int, Bank>> LoadBankMapAsync(CancellationToken cancellationToken)
        {
            var banks = await _context.Banks
                .AsNoTracking()
                .Where(b => b.ZaaerId.HasValue && b.ZaaerId.Value > 0)
                .ToListAsync(cancellationToken);

            return banks
                .Where(b => b.ZaaerId.HasValue)
                .GroupBy(b => b.ZaaerId!.Value)
                .ToDictionary(g => g.Key, g => g.First());
        }

        private async Task<Dictionary<int, (string? ImagePath, int Count)>> LoadFirstImageMapAsync(
            IReadOnlyList<int> receiptZaaerIds,
            CancellationToken cancellationToken)
        {
            if (receiptZaaerIds.Count == 0)
            {
                return new Dictionary<int, (string? ImagePath, int Count)>();
            }

            var images = await _context.DepositImages
                .AsNoTracking()
                .Where(i => receiptZaaerIds.Contains(i.ReceiptId))
                .OrderBy(i => i.ReceiptId)
                .ThenBy(i => i.DisplayOrder)
                .ThenBy(i => i.DepositImageId)
                .ToListAsync(cancellationToken);

            return images
                .GroupBy(i => i.ReceiptId)
                .ToDictionary(
                    g => g.Key,
                    g => ((string?)g.First().ImagePath, g.Count()));
        }

        private static PmsDepositRowDto MapRow(
            PaymentReceipt source,
            IReadOnlyDictionary<int, Bank> banks,
            IReadOnlyDictionary<int, (string? ImagePath, int Count)> imageMap)
        {
            banks.TryGetValue(source.BankId ?? 0, out var bank);
            var imageKey = source.ZaaerId ?? 0;
            imageMap.TryGetValue(imageKey, out var img);

            return new PmsDepositRowDto
            {
                ReceiptId = source.ReceiptId,
                ReceiptNo = source.ReceiptNo,
                ReceiptDate = source.ReceiptDate,
                AmountPaid = source.AmountPaid,
                DisplayAmount = Math.Abs(source.AmountPaid),
                BankId = source.BankId,
                BankName = source.BankName,
                BankNameAr = bank?.BankNameAr,
                BankNameEn = bank?.BankNameEn,
                PaymentMethodId = source.PaymentMethodId,
                PaymentMethod = source.PaymentMethod,
                Notes = source.Notes,
                ReceiptStatus = source.ReceiptStatus,
                FirstImageUrl = img.ImagePath,
                ImageCount = img.Count,
                CreatedAt = source.CreatedAt
            };
        }

        private static PmsDepositDetailDto MapDetail(
            PaymentReceipt source,
            IReadOnlyDictionary<int, Bank> banks,
            IReadOnlyDictionary<int, (string? ImagePath, int Count)> imageMap)
        {
            var row = MapRow(source, banks, imageMap);
            return new PmsDepositDetailDto
            {
                ReceiptId = row.ReceiptId,
                ReceiptNo = row.ReceiptNo,
                ReceiptDate = row.ReceiptDate,
                AmountPaid = row.AmountPaid,
                DisplayAmount = row.DisplayAmount,
                BankId = row.BankId,
                BankName = row.BankName,
                BankNameAr = row.BankNameAr,
                BankNameEn = row.BankNameEn,
                PaymentMethodId = row.PaymentMethodId,
                PaymentMethod = row.PaymentMethod,
                Notes = row.Notes,
                ReceiptStatus = row.ReceiptStatus,
                FirstImageUrl = row.FirstImageUrl,
                ImageCount = row.ImageCount,
                CreatedAt = row.CreatedAt
            };
        }

        private static PmsDepositSummaryDto BuildSummary(IReadOnlyList<PmsDepositRowDto> rows) =>
            new()
            {
                Count = rows.Count,
                TotalAmount = Math.Round(rows.Sum(r => r.DisplayAmount), 2, MidpointRounding.AwayFromZero)
            };

        private static bool IsCancelled(PaymentReceipt receipt) =>
            receipt.ReceiptStatus?.Trim().Equals("cancelled", StringComparison.OrdinalIgnoreCase) == true;
    }
}
