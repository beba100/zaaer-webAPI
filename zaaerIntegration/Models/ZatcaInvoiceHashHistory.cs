using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
    [Table("zatca_invoice_hash_history")]
    public class ZatcaInvoiceHashHistory
    {
        [Key]
        [Column("history_id")]
        public int HistoryId { get; set; }

        [Column("device_id")]
        public int DeviceId { get; set; }

        [Column("hotel_id")]
        public int HotelId { get; set; }

        /// <summary>invoice | credit_note | debit_note</summary>
        [Column("document_kind")]
        [MaxLength(30)]
        public string DocumentKind { get; set; } = string.Empty;

        [Column("document_id")]
        public int DocumentId { get; set; }

        [Column("document_no")]
        [MaxLength(50)]
        public string DocumentNo { get; set; } = string.Empty;

        [Column("icv")]
        public int Icv { get; set; }

        [Column("invoice_hash")]
        [MaxLength(512)]
        public string InvoiceHash { get; set; } = string.Empty;

        [Column("zatca_uuid")]
        [MaxLength(100)]
        public string ZatcaUuid { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [ForeignKey(nameof(DeviceId))]
        public ZatcaDevice Device { get; set; } = null!;
    }
}
