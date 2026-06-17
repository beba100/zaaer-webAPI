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
    public sealed class PmsHotelTargetService : IPmsHotelTargetService
    {
        private readonly MasterDbContext _masterContext;
        private readonly ApplicationDbContext _context;
        private readonly ITenantService _tenantService;
        private readonly PaymentDailyNetExTaxOptions _netExTaxOptions;

        public PmsHotelTargetService(
            MasterDbContext masterContext,
            ApplicationDbContext context,
            ITenantService tenantService,
            IOptions<PaymentDailyNetExTaxOptions> netExTaxOptions)
        {
            _masterContext = masterContext;
            _context = context;
            _tenantService = tenantService;
            _netExTaxOptions = netExTaxOptions.Value;
        }

        public async Task<PmsHotelTargetReportDto> GetTargetReportAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            var hotelZaaerId = await ResolveHotelZaaerIdAsync(cancellationToken);
            var from = fromDate.Date;
            var to = toDate.Date;
            if (to < from)
            {
                throw new InvalidOperationException("To date must be on or after from date.");
            }

            var monthYear = new DateTime(from.Year, from.Month, 1);
            var target = await _masterContext.HotelMonthlyTargets
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    t => t.HotelZaaerId == hotelZaaerId && t.MonthYear == monthYear,
                    cancellationToken);

            var metrics = await CalculateAchievementMetricsAsync(from, to, hotelZaaerId, cancellationToken);
            if (target == null)
            {
                return new PmsHotelTargetReportDto
                {
                    Target = null,
                    FromDate = from,
                    ToDate = to,
                    AchievedAmount = metrics.AchievedNetExTax,
                    AchievedGrossNet = metrics.AchievedGrossNet,
                    UsesVatOnlyNetExTax = metrics.UsesVatOnlyNetExTax,
                    AchievementPercent = 0,
                    RemainingAmount = 0,
                    HasTarget = false,
                    Tiers = Array.Empty<PmsHotelTargetTierDto>(),
                    DailyItems = metrics.DailyItems
                };
            }

            var targetDto = MapTarget(target);
            var achievedAmount = metrics.AchievedNetExTax;
            var achievementPercent = target.TargetAmount <= 0
                ? 0
                : Math.Round(achievedAmount / target.TargetAmount * 100m, 2, MidpointRounding.AwayFromZero);
            var remaining = Math.Max(0, Math.Round(target.TargetAmount - achievedAmount, 2, MidpointRounding.AwayFromZero));
            var activeRate = ResolveActiveCommissionRate(target, achievementPercent);
            var estimatedCommission = Math.Round(achievedAmount * activeRate / 100m, 2, MidpointRounding.AwayFromZero);
            var tiers = BuildTiers(target, achievementPercent, achievedAmount);

            return new PmsHotelTargetReportDto
            {
                Target = targetDto,
                FromDate = from,
                ToDate = to,
                AchievedAmount = achievedAmount,
                AchievedGrossNet = metrics.AchievedGrossNet,
                UsesVatOnlyNetExTax = metrics.UsesVatOnlyNetExTax,
                AchievementPercent = achievementPercent,
                RemainingAmount = remaining,
                ActiveCommissionRate = activeRate,
                EstimatedCommissionAmount = estimatedCommission,
                HasTarget = true,
                Tiers = tiers,
                DailyItems = metrics.DailyItems
            };
        }

        public async Task<IReadOnlyList<PmsHotelMonthlyTargetDto>> ListTargetsForCurrentHotelAsync(
            CancellationToken cancellationToken = default)
        {
            var hotelZaaerId = await ResolveHotelZaaerIdAsync(cancellationToken);
            var rows = await _masterContext.HotelMonthlyTargets
                .AsNoTracking()
                .Where(t => t.HotelZaaerId == hotelZaaerId)
                .OrderByDescending(t => t.MonthYear)
                .ThenByDescending(t => t.HotelMonthlyTargetId)
                .ToListAsync(cancellationToken);

            return rows.Select(MapTarget).ToList();
        }

        public async Task<PmsHotelMonthlyTargetDto> CreateTargetAsync(
            UpsertPmsHotelMonthlyTargetDto dto,
            CancellationToken cancellationToken = default)
        {
            var hotelZaaerId = await ResolveHotelZaaerIdAsync(cancellationToken);
            var monthYear = NormalizeMonthYear(dto.MonthYear);
            ValidateUpsert(dto);

            var exists = await _masterContext.HotelMonthlyTargets
                .AnyAsync(
                    t => t.HotelZaaerId == hotelZaaerId && t.MonthYear == monthYear,
                    cancellationToken);
            if (exists)
            {
                throw new InvalidOperationException("A target for this month already exists for this hotel.");
            }

            var branchName = await ResolveBranchNameAsync(dto.BranchName, cancellationToken);
            var now = KsaTime.Now;
            var entity = new PmsHotelMonthlyTarget
            {
                HotelZaaerId = hotelZaaerId,
                BranchName = branchName,
                MonthYear = monthYear,
                TargetAmount = Math.Round(dto.TargetAmount, 2, MidpointRounding.AwayFromZero),
                CommissionBefore85 = dto.CommissionBefore85,
                CommissionAt85 = dto.CommissionAt85,
                Commission86To100 = dto.Commission86To100,
                CreatedAt = now,
                UpdatedAt = now
            };

            _masterContext.HotelMonthlyTargets.Add(entity);
            await _masterContext.SaveChangesAsync(cancellationToken);
            return MapTarget(entity);
        }

        public async Task<PmsHotelMonthlyTargetDto> UpdateTargetAsync(
            int id,
            UpsertPmsHotelMonthlyTargetDto dto,
            CancellationToken cancellationToken = default)
        {
            var hotelZaaerId = await ResolveHotelZaaerIdAsync(cancellationToken);
            var monthYear = NormalizeMonthYear(dto.MonthYear);
            ValidateUpsert(dto);

            var entity = await _masterContext.HotelMonthlyTargets
                .FirstOrDefaultAsync(
                    t => t.HotelMonthlyTargetId == id && t.HotelZaaerId == hotelZaaerId,
                    cancellationToken)
                ?? throw new InvalidOperationException("Target not found.");

            var duplicate = await _masterContext.HotelMonthlyTargets
                .AnyAsync(
                    t => t.HotelZaaerId == hotelZaaerId
                         && t.MonthYear == monthYear
                         && t.HotelMonthlyTargetId != id,
                    cancellationToken);
            if (duplicate)
            {
                throw new InvalidOperationException("A target for this month already exists for this hotel.");
            }

            entity.MonthYear = monthYear;
            entity.TargetAmount = Math.Round(dto.TargetAmount, 2, MidpointRounding.AwayFromZero);
            entity.CommissionBefore85 = dto.CommissionBefore85;
            entity.CommissionAt85 = dto.CommissionAt85;
            entity.Commission86To100 = dto.Commission86To100;
            entity.BranchName = await ResolveBranchNameAsync(dto.BranchName, cancellationToken);
            entity.UpdatedAt = KsaTime.Now;

            await _masterContext.SaveChangesAsync(cancellationToken);
            return MapTarget(entity);
        }

        private async Task<TargetAchievementMetrics> CalculateAchievementMetricsAsync(
            DateTime from,
            DateTime to,
            int hotelZaaerId,
            CancellationToken cancellationToken)
        {
            var tenant = _tenantService.GetTenant()
                ?? throw new UnauthorizedAccessException("Tenant not resolved. Provide X-Hotel-Code.");
            var scope = await GetCurrentHotelScopeAsync(cancellationToken);
            var hotelIds = new[] { scope.LocalHotelId, scope.ScopeHotelId }.Distinct().ToList();
            var toExclusive = to.Date.AddDays(1);

            var grossNet = await _context.PaymentReceipts.AsNoTracking()
                .Where(pr =>
                    hotelIds.Contains(pr.HotelId)
                    && pr.ReceiptDate >= from
                    && pr.ReceiptDate < toExclusive
                    && pr.ReceiptStatus != null
                    && pr.ReceiptStatus.ToLower() == "paid"
                    && (pr.VoucherCode == null || pr.VoucherCode != "transfers_to_bank"))
                .SumAsync(pr => (decimal?)pr.AmountPaid, cancellationToken) ?? 0m;

            grossNet = Math.Round(grossNet, 2, MidpointRounding.AwayFromZero);

            var usesVatOnly = PaymentDailyNetExTaxHelper.RowUsesVatOnlyNetExTax(
                tenant.Code,
                tenant.Id,
                hotelZaaerId,
                _netExTaxOptions);

            var netExTax = PaymentDailyNetExTaxHelper.CalcNetExTax(grossNet, usesVatOnly);
            var dailyItems = await BuildDailyAchievementItemsAsync(
                from,
                to,
                hotelIds,
                usesVatOnly,
                cancellationToken);

            return new TargetAchievementMetrics(grossNet, netExTax, usesVatOnly, dailyItems);
        }

        private async Task<IReadOnlyList<PmsHotelTargetDailyRowDto>> BuildDailyAchievementItemsAsync(
            DateTime from,
            DateTime to,
            IReadOnlyList<int> hotelIds,
            bool usesVatOnly,
            CancellationToken cancellationToken)
        {
            var toExclusive = to.Date.AddDays(1);
            var dailyGrossRows = await _context.PaymentReceipts.AsNoTracking()
                .Where(pr =>
                    hotelIds.Contains(pr.HotelId)
                    && pr.ReceiptDate >= from
                    && pr.ReceiptDate < toExclusive
                    && pr.ReceiptStatus != null
                    && pr.ReceiptStatus.ToLower() == "paid"
                    && (pr.VoucherCode == null || pr.VoucherCode != "transfers_to_bank"))
                .GroupBy(pr => pr.ReceiptDate.Date)
                .Select(group => new
                {
                    Date = group.Key,
                    GrossNet = group.Sum(pr => pr.AmountPaid)
                })
                .ToListAsync(cancellationToken);

            var grossByDate = dailyGrossRows.ToDictionary(
                row => row.Date.Date,
                row => Math.Round(row.GrossNet, 2, MidpointRounding.AwayFromZero));

            var items = new List<PmsHotelTargetDailyRowDto>();
            for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
            {
                var gross = grossByDate.GetValueOrDefault(day, 0m);
                items.Add(new PmsHotelTargetDailyRowDto
                {
                    Date = day,
                    GrossNet = gross,
                    NetExTax = PaymentDailyNetExTaxHelper.CalcNetExTax(gross, usesVatOnly)
                });
            }

            return items;
        }

        private sealed record TargetAchievementMetrics(
            decimal AchievedGrossNet,
            decimal AchievedNetExTax,
            bool UsesVatOnlyNetExTax,
            IReadOnlyList<PmsHotelTargetDailyRowDto> DailyItems);

        private async Task<int> ResolveHotelZaaerIdAsync(CancellationToken cancellationToken)
        {
            var tenant = _tenantService.GetTenant()
                ?? throw new UnauthorizedAccessException("Tenant not resolved. Provide X-Hotel-Code.");

            if (tenant.ZaaerId is > 0)
            {
                return tenant.ZaaerId.Value;
            }

            var code = tenant.Code.Trim();
            var hotel = await _context.HotelSettings.AsNoTracking()
                .FirstOrDefaultAsync(
                    h => h.HotelCode != null && h.HotelCode.ToLower() == code.ToLower(),
                    cancellationToken)
                ?? throw new InvalidOperationException($"HotelSettings not found for code: {tenant.Code}");

            var zaaerId = hotel.ZaaerId ?? hotel.HotelId;
            if (zaaerId <= 0)
            {
                throw new InvalidOperationException("Hotel Zaaer id is not configured.");
            }

            return zaaerId;
        }

        private async Task<string?> ResolveBranchNameAsync(string? branchName, CancellationToken cancellationToken)
        {
            var arabicName = await ResolveArabicBranchNameFromHotelSettingsAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(arabicName))
            {
                return arabicName;
            }

            if (!string.IsNullOrWhiteSpace(branchName))
            {
                return branchName.Trim();
            }

            var tenant = _tenantService.GetTenant();
            if (tenant != null && !string.IsNullOrWhiteSpace(tenant.Name))
            {
                return tenant.Name.Trim();
            }

            return null;
        }

        private async Task<string?> ResolveArabicBranchNameFromHotelSettingsAsync(CancellationToken cancellationToken)
        {
            var tenant = _tenantService.GetTenant();
            var code = tenant?.Code?.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            var hotel = await _context.HotelSettings.AsNoTracking()
                .FirstOrDefaultAsync(
                    h => h.HotelCode != null && h.HotelCode.ToLower() == code.ToLower(),
                    cancellationToken);

            return string.IsNullOrWhiteSpace(hotel?.HotelName) ? null : hotel.HotelName.Trim();
        }

        private static DateTime NormalizeMonthYear(DateTime value) =>
            new(value.Year, value.Month, 1);

        private static void ValidateUpsert(UpsertPmsHotelMonthlyTargetDto dto)
        {
            if (dto.TargetAmount <= 0)
            {
                throw new InvalidOperationException("Target amount is required and must be greater than zero.");
            }

            if (dto.CommissionBefore85 <= 0 || dto.CommissionAt85 <= 0 || dto.Commission86To100 <= 0)
            {
                throw new InvalidOperationException("All commission rates are required and must be greater than zero.");
            }
        }

        private static decimal ResolveActiveCommissionRate(PmsHotelMonthlyTarget target, decimal achievementPercent)
        {
            if (achievementPercent >= 86m)
            {
                return target.Commission86To100;
            }

            if (achievementPercent >= 85m)
            {
                return target.CommissionAt85;
            }

            return target.CommissionBefore85;
        }

        private static IReadOnlyList<PmsHotelTargetTierDto> BuildTiers(
            PmsHotelMonthlyTarget target,
            decimal achievementPercent,
            decimal achievedAmount)
        {
            var targetAmount = target.TargetAmount;
            var cap85 = Math.Round(targetAmount * 0.85m, 2, MidpointRounding.AwayFromZero);
            var cap86 = Math.Round(targetAmount * 0.86m, 2, MidpointRounding.AwayFromZero);

            decimal TierAmount(decimal cap) => Math.Round(Math.Min(achievedAmount, cap), 2, MidpointRounding.AwayFromZero);

            return new[]
            {
                new PmsHotelTargetTierDto
                {
                    TierKey = "before_85",
                    Label = "before_85",
                    CommissionRate = target.CommissionBefore85,
                    ThresholdMinPercent = 0,
                    ThresholdMaxPercent = 84.99m,
                    IsReached = achievementPercent >= 85m,
                    IsActive = achievementPercent < 85m,
                    TierAchievedAmount = TierAmount(cap85),
                    CommissionAmount = Math.Round(TierAmount(cap85) * target.CommissionBefore85 / 100m, 2, MidpointRounding.AwayFromZero)
                },
                new PmsHotelTargetTierDto
                {
                    TierKey = "at_85",
                    Label = "at_85",
                    CommissionRate = target.CommissionAt85,
                    ThresholdMinPercent = 85m,
                    ThresholdMaxPercent = 85.99m,
                    IsReached = achievementPercent >= 86m,
                    IsActive = achievementPercent >= 85m && achievementPercent < 86m,
                    TierAchievedAmount = achievementPercent >= 85m
                        ? Math.Round(Math.Min(achievedAmount, cap86) - cap85, 2, MidpointRounding.AwayFromZero)
                        : 0,
                    CommissionAmount = achievementPercent >= 85m
                        ? Math.Round(Math.Max(0, Math.Min(achievedAmount, cap86) - cap85) * target.CommissionAt85 / 100m, 2, MidpointRounding.AwayFromZero)
                        : 0
                },
                new PmsHotelTargetTierDto
                {
                    TierKey = "86_to_100",
                    Label = "86_to_100",
                    CommissionRate = target.Commission86To100,
                    ThresholdMinPercent = 86m,
                    ThresholdMaxPercent = 100m,
                    IsReached = achievementPercent >= 100m,
                    IsActive = achievementPercent >= 86m && achievementPercent < 100m,
                    TierAchievedAmount = achievementPercent >= 86m
                        ? Math.Round(Math.Max(0, achievedAmount - cap86), 2, MidpointRounding.AwayFromZero)
                        : 0,
                    CommissionAmount = achievementPercent >= 86m
                        ? Math.Round(Math.Max(0, achievedAmount - cap86) * target.Commission86To100 / 100m, 2, MidpointRounding.AwayFromZero)
                        : 0
                }
            };
        }

        private static PmsHotelMonthlyTargetDto MapTarget(PmsHotelMonthlyTarget entity) =>
            new()
            {
                HotelMonthlyTargetId = entity.HotelMonthlyTargetId,
                HotelZaaerId = entity.HotelZaaerId,
                BranchName = entity.BranchName,
                MonthYear = entity.MonthYear,
                TargetAmount = entity.TargetAmount,
                CommissionBefore85 = entity.CommissionBefore85,
                CommissionAt85 = entity.CommissionAt85,
                Commission86To100 = entity.Commission86To100,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };

        private sealed record HotelScope(int LocalHotelId, int ScopeHotelId);

        private async Task<HotelScope> GetCurrentHotelScopeAsync(CancellationToken cancellationToken)
        {
            var tenant = _tenantService.GetTenant()
                ?? throw new UnauthorizedAccessException("Tenant not resolved. Provide X-Hotel-Code.");
            var code = tenant.Code.Trim();
            var hotel = await _context.HotelSettings.AsNoTracking()
                .Where(h => h.HotelCode != null)
                .FirstOrDefaultAsync(h => h.HotelCode!.ToLower() == code.ToLower(), cancellationToken)
                ?? throw new InvalidOperationException($"HotelSettings not found for code: {tenant.Code}");

            return new HotelScope(hotel.HotelId, hotel.ZaaerId ?? hotel.HotelId);
        }
    }
}
