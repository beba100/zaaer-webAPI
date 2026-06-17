using System.ComponentModel.DataAnnotations;

namespace zaaerIntegration.DTOs.Pms
{
    /// <summary>
    /// PMS create payment receipt — receipt number is allocated server-side via Master DB numbering.
    /// </summary>
    public sealed class PmsCreatePaymentReceiptDto
    {
        [Required]
        public int HotelId { get; set; }

        /// <summary>Internal <c>reservations.reservation_id</c>.</summary>
        [Required]
        public int ReservationId { get; set; }

        public int? CustomerId { get; set; }

        public int? UnitId { get; set; }

        /// <summary>
        /// <c>receipt</c> | <c>refund</c> (legacy UI may still send <c>security_deposit</c> / <c>security_deposit_refund</c>).
        /// </summary>
        [Required]
        [StringLength(50)]
        public string ReceiptType { get; set; } = "receipt";

        /// <summary>
        /// Business voucher: <c>receipt</c>, <c>security_deposit</c>, <c>refund</c>, <c>security_deposit_refund</c>.
        /// When set, overrides voucher derived from legacy <see cref="ReceiptType"/> alone.
        /// </summary>
        [StringLength(50)]
        public string? VoucherCode { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal AmountPaid { get; set; }

        public DateTime? ReceiptDate { get; set; }

        public int? PaymentMethodId { get; set; }

        public int? BankId { get; set; }

        [StringLength(100)]
        public string? TransactionNo { get; set; }

        /// <summary>Business reason line (e.g. rental fees for reservation).</summary>
        [StringLength(500)]
        public string? Reason { get; set; }

        /// <summary>Rent period start (rent receipts only).</summary>
        public DateTime? ReceiptFrom { get; set; }

        /// <summary>Rent period end (rent receipts only).</summary>
        public DateTime? ReceiptTo { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        /// <summary>Ignored on create — server sets <c>pms_users.user_id</c> from JWT.</summary>
        public int? CreatedBy { get; set; }

        /// <summary>When set, creates a promissory collection receipt (does not affect rent balance).</summary>
        public int? PromissoryNoteZaaerId { get; set; }

        /// <summary>Rent receipt only — building guard rent (requires <c>payments.building_guard_rent</c>).</summary>
        public bool IsBuildingGuardRent { get; set; }
    }
}
