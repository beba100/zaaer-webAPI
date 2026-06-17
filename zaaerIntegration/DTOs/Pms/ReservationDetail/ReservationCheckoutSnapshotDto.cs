#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.Pms.ReservationDetail
{
    /// <summary>
    /// Authoritative reservation financial snapshot for check-out (recomputed from DB lines and receipts).
    /// </summary>
    public sealed class ReservationCheckoutSnapshotDto
    {
        public int ReservationId { get; set; }
        public int? ZaaerId { get; set; }

        public decimal RentTotal { get; set; }
        public decimal ExtrasTotal { get; set; }
        public decimal PenaltiesTotal { get; set; }
        public decimal DiscountsTotal { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal BalanceAmount { get; set; }

        /// <summary>Sum of active (non-void) invoice totals (gross).</summary>
        public decimal GrossInvoicedTotal { get; set; }

        /// <summary>Active credit notes linked to reservation invoices (reduces net invoiced).</summary>
        public decimal CreditNotesTotal { get; set; }

        /// <summary>Active debit notes linked to reservation invoices.</summary>
        public decimal DebitNotesTotal { get; set; }

        /// <summary>Gross invoiced minus credits plus debits.</summary>
        public decimal NetInvoicedTotal { get; set; }

        /// <summary>Alias for <see cref="NetInvoicedTotal"/> (backward compatibility).</summary>
        public decimal InvoicedTotal { get; set; }

        /// <summary>Net billable folio capped by collected rent when paid.</summary>
        public decimal InvoiceRequiredAmount { get; set; }

        /// <summary>Remaining amount that should still be invoiced before check-out.</summary>
        public decimal InvoiceRemaining { get; set; }
    }
}
