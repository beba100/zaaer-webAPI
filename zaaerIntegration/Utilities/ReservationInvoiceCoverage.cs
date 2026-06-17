using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.Services.Implementations;

namespace zaaerIntegration.Utilities
{
    /// <summary>
    /// Net invoiced vs folio/paid coverage for a reservation (includes credit/debit notes).
    /// </summary>
    public static class ReservationInvoiceCoverage
    {
        public sealed record CoverageResult(
            decimal GrossInvoiced,
            decimal CreditNotesTotal,
            decimal DebitNotesTotal,
            decimal NetInvoiced,
            decimal FolioNetAmount,
            decimal AmountPaid,
            decimal InvoiceRequiredAmount,
            decimal InvoiceRemaining);

        public static async Task<CoverageResult> ComputeAsync(
            ApplicationDbContext context,
            int hotelId,
            int internalReservationId,
            int? zaaerId,
            decimal totalAmount,
            decimal discounts,
            decimal amountPaid,
            CancellationToken cancellationToken = default)
        {
            var keys = ReservationFinancialSyncService.BuildReservationKeys(internalReservationId, zaaerId);

            var invoices = await context.Invoices
                .AsNoTracking()
                .Where(i =>
                    i.HotelId == hotelId
                    && i.ReservationId.HasValue
                    && keys.Contains(i.ReservationId.Value))
                .ToListAsync(cancellationToken);

            var activeInvoices = invoices
                .Where(i => !PmsInvoiceService.IsVoidedPaymentStatus(i.PaymentStatus))
                .ToList();

            var grossInvoiced = Math.Round(
                activeInvoices.Sum(i => i.TotalAmount ?? 0m),
                2,
                MidpointRounding.AwayFromZero);

            var invoiceFks = activeInvoices
                .Select(PmsInvoiceService.ResolveInvoiceForeignKey)
                .Distinct()
                .ToList();

            var creditNotes = await context.CreditNotes
                .AsNoTracking()
                .Where(c =>
                    c.HotelId == hotelId
                    && ((c.ReservationId.HasValue && keys.Contains(c.ReservationId.Value))
                        || invoiceFks.Contains(c.InvoiceId)))
                .ToListAsync(cancellationToken);

            var debitNotes = await context.DebitNotes
                .AsNoTracking()
                .Where(d =>
                    d.HotelId == hotelId
                    && ((d.ReservationId.HasValue && keys.Contains(d.ReservationId.Value))
                        || invoiceFks.Contains(d.InvoiceId)))
                .ToListAsync(cancellationToken);

            var creditTotal = Math.Round(
                creditNotes.Sum(c => c.CreditAmount),
                2,
                MidpointRounding.AwayFromZero);
            var debitTotal = Math.Round(
                debitNotes.Sum(d => d.DebitAmount),
                2,
                MidpointRounding.AwayFromZero);

            var netInvoiced = Math.Round(
                Math.Max(0m, grossInvoiced - creditTotal + debitTotal),
                2,
                MidpointRounding.AwayFromZero);

            var folioNet = Math.Round(totalAmount - discounts, 2, MidpointRounding.AwayFromZero);
            var paid = Math.Round(amountPaid, 2, MidpointRounding.AwayFromZero);

            // Invoice up to folio net, capped by collected rent when payments exist.
            var invoiceRequired = paid > 0.01m
                ? Math.Min(folioNet, paid)
                : folioNet;

            var invoiceRemaining = Math.Round(
                Math.Max(0m, invoiceRequired - netInvoiced),
                2,
                MidpointRounding.AwayFromZero);

            return new CoverageResult(
                grossInvoiced,
                creditTotal,
                debitTotal,
                netInvoiced,
                folioNet,
                paid,
                invoiceRequired,
                invoiceRemaining);
        }
    }
}
