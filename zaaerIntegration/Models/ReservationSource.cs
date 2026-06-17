using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Reservation channel/source (Reception, OTAs, etc.) — tenant table <c>sources</c>; <see cref="Code"/> aligns with <c>reservations.source</c>.
    /// </summary>
    [Table("sources")]
    public class ReservationSource
    {
        [Key]
        [Column("source_id")]
        public int SourceId { get; set; }

        /// <summary>Stable key stored on <c>reservations.source</c> (e.g. Reception, Booking.com).</summary>
        [Required]
        [StringLength(100)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        [Column("name_ar")]
        public string? NameAr { get; set; }

        /// <summary><c>primary</c> (e.g. Reception) or <c>OTA</c>, etc.</summary>
        [Required]
        [StringLength(50)]
        [Column("source_type")]
        public string SourceType { get; set; } = "OTA";

        [StringLength(200)]
        [Column("report_name")]
        public string? ReportName { get; set; }

        [Column("commission_pct", TypeName = "decimal(6,2)")]
        public decimal CommissionPct { get; set; }

        [StringLength(500)]
        [Column("url")]
        public string? Url { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        /// <summary>Lower sorts first; Reception should use <c>0</c>.</summary>
        [Column("sort_order")]
        public int SortOrder { get; set; } = 100;
    }
}
