using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Pricing period inside a reservation. Each period can have its own rental type and rate.
    /// </summary>
    [Table("reservation_periods")]
    public class ReservationPeriod
    {
        [Key]
        [Column("period_id")]
        public int PeriodId { get; set; }

        [Column("reservation_id")]
        [Required]
        public int ReservationId { get; set; }

        [Column("unit_id")]
        public int? UnitId { get; set; }

        [Column("rental_type")]
        [Required]
        [MaxLength(30)]
        public string RentalType { get; set; } = FinanceLedgerAPI.Enums.RentalType.Daily.ToString();

        [Column("from_date")]
        [Required]
        public DateTime FromDate { get; set; }

        [Column("to_date")]
        [Required]
        public DateTime ToDate { get; set; }

        [Column("gross_rate", TypeName = "decimal(12,2)")]
        [Required]
        public decimal GrossRate { get; set; }

        [Column("tax_included")]
        public bool TaxIncluded { get; set; } = true;

        [Column("status")]
        [MaxLength(30)]
        public string Status { get; set; } = "Active";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
