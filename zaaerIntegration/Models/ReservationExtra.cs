using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Extra / add-on line on a reservation (packages, posting rules, amounts) — <c>dbo.reservation_extras</c>.
    /// <see cref="ReservationId"/> stores the booking key used for grouping: <c>zaaer_id</c> when set, otherwise internal <c>reservations.reservation_id</c> (no FK to reservations).
    /// Optional <see cref="UnitId"/> is <c>reservation_units.unit_id</c> (room line), when set.
    /// </summary>
    [Table("reservation_extras")]
    public class ReservationExtra
    {
        [Key]
        [Column("extra_id")]
        public int ExtraId { get; set; }

        /// <summary>External or internal reservation key (see class summary); not a FK to <c>reservations</c>.</summary>
        [Column("reservation_id")]
        public int ReservationId { get; set; }

        /// <summary>Optional FK to <c>reservation_units.unit_id</c> (room line).</summary>
        [Column("unit_id")]
        public int? UnitId { get; set; }

        /// <summary>Optional future FK to a packages catalog (no FK until table exists).</summary>
        [Column("package_id")]
        public int? PackageId { get; set; }

        [MaxLength(400)]
        [Column("item_name")]
        public string? ItemName { get; set; }

        /// <summary>e.g. OnCheckIn, OnCustomDate (matches UI posting rule).</summary>
        [Required]
        [MaxLength(80)]
        [Column("posting_rule")]
        public string PostingRule { get; set; } = "OnCheckIn";

        [Column("service_date")]
        public DateTime? ServiceDate { get; set; }

        [Column("guest_count")]
        public int? GuestCount { get; set; }

        [Column("night_count")]
        public int? NightCount { get; set; }

        [Column("unit_price", TypeName = "decimal(12,2)")]
        public decimal UnitPrice { get; set; }

        [Column("subtotal", TypeName = "decimal(12,2)")]
        public decimal Subtotal { get; set; }

        [Column("tax_amount", TypeName = "decimal(12,2)")]
        public decimal TaxAmount { get; set; }

        [Column("total_amount", TypeName = "decimal(12,2)")]
        public decimal TotalAmount { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
