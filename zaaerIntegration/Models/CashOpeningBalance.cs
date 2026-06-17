using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
    [Table("cash_opening_balance")]
    public sealed class CashOpeningBalance
    {
        [Key]
        [Column("opening_id")]
        public int OpeningId { get; set; }

        [Column("hotel_id")]
        public int HotelId { get; set; }

        [Column("opening_date", TypeName = "date")]
        public DateTime OpeningDate { get; set; }

        [Column("opening_amount", TypeName = "decimal(18,2)")]
        public decimal OpeningAmount { get; set; }

        [Column("notes")]
        [MaxLength(255)]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }
    }
}
