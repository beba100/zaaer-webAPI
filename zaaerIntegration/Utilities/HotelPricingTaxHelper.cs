using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;

namespace zaaerIntegration.Utilities
{
    /// <summary>
    /// Hotel VAT / lodging (EWA) pricing — same rules as <see cref="Services.Implementations.ReservationDetailService"/>.
    /// </summary>
    public sealed record HotelPricingTaxConfig(
        decimal VatRate,
        bool VatIncluded,
        decimal EwaRate,
        bool EwaIncluded)
    {
        public bool TaxIncluded => VatIncluded && EwaIncluded;
    }

    public static class HotelPricingTaxHelper
    {
        public static async Task<HotelPricingTaxConfig> GetConfigAsync(
            ApplicationDbContext context,
            int hotelId,
            CancellationToken cancellationToken = default)
        {
            var taxes = await context.Taxes.AsNoTracking()
                .Where(t => t.HotelId == hotelId && t.Enabled)
                .ToListAsync(cancellationToken);

            var vat = taxes
                .Where(IsVatTaxRow)
                .OrderByDescending(t => t.TaxRate)
                .FirstOrDefault();

            var ewa = taxes
                .Where(IsEwaTaxRow)
                .OrderByDescending(t => t.TaxRate)
                .FirstOrDefault();

            return new HotelPricingTaxConfig(
                vat?.TaxRate ?? 0m,
                vat?.TaxIncluded ?? true,
                ewa?.TaxRate ?? 0m,
                ewa?.TaxIncluded ?? true);
        }

        /// <summary>
        /// POS / outlet sales: VAT from <c>taxes</c> (e.g. apply_on Extras); lodging (EWA) applies to Rent only — exclude EWA rate.
        /// Inclusive VAT 15%: gross 100 → net 86.96 + VAT 13.04.
        /// </summary>
        public static async Task<HotelPricingTaxConfig> GetPosConfigAsync(
            ApplicationDbContext context,
            int hotelId,
            CancellationToken cancellationToken = default)
        {
            var config = await GetConfigAsync(context, hotelId, cancellationToken);
            return config with { EwaRate = 0m };
        }

        public static (decimal NetAmount, decimal EwaAmount, decimal VatAmount, decimal Total) CalculateAmounts(
            decimal grossRate,
            HotelPricingTaxConfig taxConfig)
        {
            var gross = Math.Round(grossRate, 2, MidpointRounding.AwayFromZero);
            var vatRate = taxConfig.VatRate / 100m;
            var ewaRate = taxConfig.EwaRate / 100m;

            if (vatRate <= 0m && ewaRate <= 0m)
            {
                return (gross, 0m, 0m, gross);
            }

            if (taxConfig.EwaIncluded && taxConfig.VatIncluded)
            {
                var lr = ewaRate;
                var vr = vatRate;
                var divisor = 1m + lr + (1m + lr) * vr;
                if (divisor == 0m)
                {
                    return (gross, 0m, 0m, gross);
                }

                var net = Math.Round(gross / divisor, 2, MidpointRounding.AwayFromZero);
                var ewa = Math.Round(net * lr, 2, MidpointRounding.AwayFromZero);
                var vat = Math.Round((net + ewa) * vr, 2, MidpointRounding.AwayFromZero);
                var total = Math.Round(net + ewa + vat, 2, MidpointRounding.AwayFromZero);
                var drift = Math.Round(gross - total, 2, MidpointRounding.AwayFromZero);
                if (drift != 0m)
                {
                    vat = Math.Round(vat + drift, 2, MidpointRounding.AwayFromZero);
                    total = gross;
                }

                return (net, ewa, vat, total);
            }

            var addedEwa = Math.Round(gross * ewaRate, 2, MidpointRounding.AwayFromZero);
            var vatBase = gross + addedEwa;
            var addedVat = Math.Round(vatBase * vatRate, 2, MidpointRounding.AwayFromZero);
            return (gross, addedEwa, addedVat, Math.Round(gross + addedEwa + addedVat, 2, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// POS order totals: shelf/line gross uses hotel <c>taxes</c> rows (VAT/EWA, tax_included) — same as reservations.
        /// Inclusive: total 100 → subtotal 86.96 + tax 13.04; exclusive: subtotal 100 + tax 15 → total 115.
        /// </summary>
        public static (decimal Subtotal, decimal TaxAmount, decimal Total) ComputePosOrderTotals(
            IEnumerable<decimal> lineGrossAmounts,
            decimal discountAmount,
            HotelPricingTaxConfig taxConfig)
        {
            decimal grossSum = 0;
            foreach (var grossRaw in lineGrossAmounts)
            {
                grossSum += Math.Round(Math.Max(0, grossRaw), 2, MidpointRounding.AwayFromZero);
            }

            grossSum = Math.Round(grossSum, 2, MidpointRounding.AwayFromZero);
            var discount = Math.Round(Math.Max(0, discountAmount), 2, MidpointRounding.AwayFromZero);
            if (discount > grossSum)
            {
                discount = grossSum;
            }

            var adjustedGross = Math.Round(grossSum - discount, 2, MidpointRounding.AwayFromZero);
            if (adjustedGross <= 0)
            {
                return (0, 0, 0);
            }

            var calc = CalculateAmounts(adjustedGross, taxConfig);
            return (
                calc.NetAmount,
                Math.Round(calc.EwaAmount + calc.VatAmount, 2, MidpointRounding.AwayFromZero),
                calc.Total);
        }

        private static bool IsVatTaxRow(Tax tax)
        {
            var taxType = tax.TaxType ?? string.Empty;
            var taxName = tax.TaxName ?? string.Empty;
            return taxType.Equals("vat", StringComparison.OrdinalIgnoreCase) ||
                   taxName.Contains("vat", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEwaTaxRow(Tax tax)
        {
            var taxType = (tax.TaxType ?? string.Empty).ToLowerInvariant();
            var taxName = (tax.TaxName ?? string.Empty).ToLowerInvariant();
            return taxType is "ewa" or "lodging" or "lodging_tax" or "lodgingtax" ||
                   taxName.Contains("ewa", StringComparison.OrdinalIgnoreCase) ||
                   taxName.Contains("lodging", StringComparison.OrdinalIgnoreCase);
        }
    }
}
