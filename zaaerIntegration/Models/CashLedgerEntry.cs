using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Immutable cash movement ledger. Business tables remain the source of document details.
    /// </summary>
    [Table("cash_ledger")]
    public sealed class CashLedgerEntry
    {
        [Key]
        [Column("ledger_id")]
        public long LedgerId { get; set; }

        [Column("hotel_id")]
        public int HotelId { get; set; }

        [Column("cash_box_id")]
        public int? CashBoxId { get; set; }

        [Column("branch_id")]
        public int? BranchId { get; set; }

        [Column("transaction_date")]
        public DateTime TransactionDate { get; set; }

        [Column("source_type")]
        [MaxLength(50)]
        public string SourceType { get; set; } = string.Empty;

        [Column("source_subtype")]
        [MaxLength(50)]
        public string? SourceSubtype { get; set; }

        [Column("source_id")]
        public long? SourceId { get; set; }

        [Column("source_zaaer_id")]
        public long? SourceZaaerId { get; set; }

        [Column("source_no")]
        [MaxLength(50)]
        public string? SourceNo { get; set; }

        [Column("movement_label")]
        [MaxLength(100)]
        public string? MovementLabel { get; set; }

        [Column("debit_amount", TypeName = "decimal(18,2)")]
        public decimal DebitAmount { get; set; }

        [Column("credit_amount", TypeName = "decimal(18,2)")]
        public decimal CreditAmount { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [Column("balance_amount", TypeName = "decimal(18,2)")]
        public decimal BalanceAmount { get; private set; }

        [Column("description")]
        [MaxLength(500)]
        public string? Description { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = "posted";

        [Column("reversal_of_ledger_id")]
        public long? ReversalOfLedgerId { get; set; }

        [Column("idempotency_key")]
        [MaxLength(200)]
        public string IdempotencyKey { get; set; } = string.Empty;
    }
}
