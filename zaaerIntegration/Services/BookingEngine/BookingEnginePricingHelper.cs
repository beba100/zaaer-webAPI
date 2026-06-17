using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;

namespace zaaerIntegration.Services.BookingEngine
{
    internal static class BookingEnginePricingHelper
    {
        internal sealed record TaxConfig(decimal VatRate, bool VatIncluded, decimal EwaRate, bool EwaIncluded);

        internal static async Task<TaxConfig> GetTaxConfigAsync(ApplicationDbContext context, int hotelId, CancellationToken ct)
        {
            var taxes = await context.Taxes.AsNoTracking()
                .Where(t => t.HotelId == hotelId && t.Enabled)
                .ToListAsync(ct);

            var vat = taxes.Where(IsVat).OrderByDescending(t => t.TaxRate).FirstOrDefault();
            var ewa = taxes.Where(IsEwa).OrderByDescending(t => t.TaxRate).FirstOrDefault();

            return new TaxConfig(
                vat?.TaxRate ?? 0m,
                vat?.TaxIncluded ?? true,
                ewa?.TaxRate ?? 0m,
                ewa?.TaxIncluded ?? true);
        }

        internal static (decimal Net, decimal Ewa, decimal Vat, decimal Total) Calculate(decimal gross, TaxConfig tax)
        {
            var g = Math.Round(gross, 2, MidpointRounding.AwayFromZero);
            var vatRate = tax.VatRate / 100m;
            var ewaRate = tax.EwaRate / 100m;

            if (vatRate <= 0m && ewaRate <= 0m)
            {
                return (g, 0m, 0m, g);
            }

            if (tax.EwaIncluded && tax.VatIncluded)
            {
                var divisor = 1m + ewaRate + (1m + ewaRate) * vatRate;
                if (divisor == 0m)
                {
                    return (g, 0m, 0m, g);
                }

                var net = Math.Round(g / divisor, 2, MidpointRounding.AwayFromZero);
                var ewa = Math.Round(net * ewaRate, 2, MidpointRounding.AwayFromZero);
                var vat = Math.Round((net + ewa) * vatRate, 2, MidpointRounding.AwayFromZero);
                var total = Math.Round(net + ewa + vat, 2, MidpointRounding.AwayFromZero);
                var drift = Math.Round(g - total, 2, MidpointRounding.AwayFromZero);
                if (drift != 0m)
                {
                    vat = Math.Round(vat + drift, 2, MidpointRounding.AwayFromZero);
                    total = g;
                }

                return (net, ewa, vat, total);
            }

            var netEx = g;
            var ewaEx = Math.Round(netEx * ewaRate, 2, MidpointRounding.AwayFromZero);
            var vatEx = Math.Round((netEx + ewaEx) * vatRate, 2, MidpointRounding.AwayFromZero);
            var totalEx = Math.Round(netEx + ewaEx + vatEx, 2, MidpointRounding.AwayFromZero);
            return (netEx, ewaEx, vatEx, totalEx);
        }

        internal static int CountNights(DateTime checkIn, DateTime checkOut)
        {
            if (checkOut <= checkIn)
            {
                return 1;
            }

            return Math.Max(1, (int)(checkOut.Date - checkIn.Date).TotalDays);
        }

        private static bool IsVat(Tax tax)
        {
            var taxType = tax.TaxType ?? string.Empty;
            var taxName = tax.TaxName ?? string.Empty;
            return taxType.Equals("vat", StringComparison.OrdinalIgnoreCase) ||
                   taxName.Contains("vat", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEwa(Tax tax)
        {
            var taxType = (tax.TaxType ?? string.Empty).ToLowerInvariant();
            var taxName = (tax.TaxName ?? string.Empty).ToLowerInvariant();
            return taxType is "ewa" or "lodging" or "lodging_tax" or "lodgingtax" ||
                   taxName.Contains("ewa", StringComparison.OrdinalIgnoreCase) ||
                   taxName.Contains("lodging", StringComparison.OrdinalIgnoreCase);
        }
    }
}
