using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Promissory note (كمبيالة / سند لأمر) linked to a reservation.
    /// </summary>
    [Table("promissory_notes")]
    public class PromissoryNote
    {
        [Key]
        [Column("promissory_note_id")]
        public int PromissoryNoteId { get; set; }

        [Column("promissory_no")]
        [Required]
        [MaxLength(50)]
        public string PromissoryNo { get; set; } = string.Empty;

        [Column("zaaer_id")]
        public int? ZaaerId { get; set; }

        [Column("hotel_id")]
        public int HotelId { get; set; }

        /// <summary>Stored as <c>reservations.zaaer_id</c> (same as payment_receipts).</summary>
        [Column("reservation_id")]
        public int? ReservationId { get; set; }

        [Column("customer_id")]
        public int? CustomerId { get; set; }

        [Column("corporate_id")]
        public int? CorporateId { get; set; }

        [Column("payable_to")]
        [MaxLength(200)]
        public string? PayableTo { get; set; }

        [Column("reason")]
        [MaxLength(500)]
        public string? Reason { get; set; }

        [Column("place_of_maturity")]
        [MaxLength(200)]
        public string? PlaceOfMaturity { get; set; }

        [Column("maturity_date", TypeName = "date")]
        public DateTime MaturityDate { get; set; }

        [Column("amount", TypeName = "decimal(12,2)")]
        public decimal Amount { get; set; }

        [Column("amount_collected", TypeName = "decimal(12,2)")]
        public decimal AmountCollected { get; set; }

        [Column("status")]
        [MaxLength(30)]
        public string Status { get; set; } = "open";

        [Column("payment_link_sent")]
        public bool PaymentLinkSent { get; set; }

        [Column("notes")]
        [MaxLength(1000)]
        public string? Notes { get; set; }

        [Column("collection_receipt_id")]
        public int? CollectionReceiptId { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
