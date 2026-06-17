using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Per-night / per-month gross rate lines for PMS pricing.
    /// <see cref="ReservationId"/> and <see cref="UnitId"/> store integration keys when set
    /// (reservation <c>zaaer_id</c>, unit <c>zaaer_id</c> or apartment <c>zaaer_id</c>), else internal PKs — not FK-bound to <c>reservations</c>/<c>reservation_units</c>.
    /// </summary>
    [Table("reservation_unit_day_rates")]
    public class ReservationUnitDayRate
    {
        [Key]
        [Column("rate_id")]
        public int RateId { get; set; }

        /// <summary>Global reservation key: <c>zaaer_id</c> when set, else <c>reservations.reservation_id</c>.</summary>
        [Column("reservation_id")]
        [Required]
        public int ReservationId { get; set; }

        /// <summary>
        /// Global apartment key: <c>apartments.zaaer_id</c> when set, else <c>apartments.apartment_id</c>,
        /// resolved via <c>reservation_units.apartment_id</c> (not <c>reservation_units.unit_id</c>).
        /// </summary>
        [Column("unit_id")]
        [Required]
        public int UnitId { get; set; }

        [Column("night_date")]
        [Required]
        public DateTime NightDate { get; set; }

        [Column("gross_rate", TypeName = "decimal(12,2)")]
        [Required]
        public decimal GrossRate { get; set; }

        [Column("ewa_amount", TypeName = "decimal(12,2)")]
        public decimal? EwaAmount { get; set; }

        [Column("vat_amount", TypeName = "decimal(12,2)")]
        public decimal? VatAmount { get; set; }

        [Column("net_amount", TypeName = "decimal(12,2)")]
        public decimal? NetAmount { get; set; }

        [Column("is_manual")]
        public bool IsManual { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Zaaer System ID (معرف Zaaer)
        /// External ID from Zaaer integration system
        /// </summary>
        [Column("zaaer_id")]
        public int? ZaaerId { get; set; }

        [NotMapped]
        public Reservation? Reservation { get; set; }

        [NotMapped]
        public ReservationUnit? ReservationUnit { get; set; }
    }
}


