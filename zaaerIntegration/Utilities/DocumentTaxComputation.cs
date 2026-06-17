using FinanceLedgerAPI.Models;
using zaaerIntegration.Data;

namespace zaaerIntegration.Utilities
{
    internal static class DocumentTaxComputation
    {
        public static async Task ApplyInvoiceTaxesAsync(
            ApplicationDbContext context,
            Invoice invoice,
            CancellationToken cancellationToken = default)
        {
            if (invoice.HotelId <= 0)
            {
                return;
            }

            var total = Math.Round(invoice.TotalAmount ?? 0m, 2, MidpointRounding.AwayFromZero);
            var config = await HotelPricingTaxHelper.GetConfigAsync(context, invoice.HotelId, cancellationToken);
            var calc = HotelPricingTaxHelper.CalculateAmounts(total, config);
            invoice.Subtotal = calc.NetAmount;
            invoice.LodgingTaxRate = config.EwaRate;
            invoice.LodgingTaxAmount = calc.EwaAmount;
            invoice.VatRate = config.VatRate;
            invoice.VatAmount = calc.VatAmount;
            invoice.TotalAmount = calc.Total;
        }

        public static async Task ApplyCreditNoteTaxesAsync(
            ApplicationDbContext context,
            CreditNote creditNote,
            CancellationToken cancellationToken = default)
        {
            if (creditNote.HotelId <= 0)
            {
                return;
            }

            var total = Math.Round(creditNote.CreditAmount, 2, MidpointRounding.AwayFromZero);
            var config = await HotelPricingTaxHelper.GetConfigAsync(context, creditNote.HotelId, cancellationToken);
            var calc = HotelPricingTaxHelper.CalculateAmounts(total, config);
            creditNote.Subtotal = calc.NetAmount;
            creditNote.LodgingTaxRate = config.EwaRate;
            creditNote.LodgingTaxAmount = calc.EwaAmount;
            creditNote.VatRate = config.VatRate;
            creditNote.VatAmount = calc.VatAmount;
            creditNote.CreditAmount = calc.Total;
        }

        public static async Task ApplyDebitNoteTaxesAsync(
            ApplicationDbContext context,
            DebitNote debitNote,
            CancellationToken cancellationToken = default)
        {
            if (debitNote.HotelId <= 0)
            {
                return;
            }

            var total = Math.Round(debitNote.DebitAmount, 2, MidpointRounding.AwayFromZero);
            var config = await HotelPricingTaxHelper.GetConfigAsync(context, debitNote.HotelId, cancellationToken);
            var calc = HotelPricingTaxHelper.CalculateAmounts(total, config);
            debitNote.Subtotal = calc.NetAmount;
            debitNote.LodgingTaxRate = config.EwaRate;
            debitNote.LodgingTaxAmount = calc.EwaAmount;
            debitNote.VatRate = config.VatRate;
            debitNote.VatAmount = calc.VatAmount;
            debitNote.DebitAmount = calc.Total;
        }
    }
}
