using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FinanceLedgerAPI.Models;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Models
{
    /// <summary>
    /// Images attached to bank deposit receipts (<c>payment_receipts</c> with <c>transfers_to_bank</c>).
    /// </summary>
    [Table("deposit_images")]
    public class DepositImage
    {
        [Key]
        [Column("deposit_image_id")]
        public int DepositImageId { get; set; }

        /// <summary>
        /// Links to <c>payment_receipts.zaaer_id</c> (integration id), not <c>receipt_id</c> PK.
        /// </summary>
        [Column("receipt_id")]
        [Required]
        public int ReceiptId { get; set; }

        [Column("image_path")]
        [Required]
        [MaxLength(500)]
        public string ImagePath { get; set; } = string.Empty;

        [Column("original_filename")]
        [MaxLength(255)]
        public string? OriginalFilename { get; set; }

        [Column("file_size")]
        public long? FileSize { get; set; }

        [Column("content_type")]
        [MaxLength(100)]
        public string? ContentType { get; set; }

        [Column("display_order")]
        public int DisplayOrder { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [ForeignKey(nameof(ReceiptId))]
        public PaymentReceipt? PaymentReceipt { get; set; }
    }
}
