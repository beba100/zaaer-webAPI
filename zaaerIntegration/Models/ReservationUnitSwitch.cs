using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
    [Table("reservation_unit_swaps")]
    public class ReservationUnitSwitch
    {
        [Key]
        [Column("switch_id")]
        public int SwitchId { get; set; }

        /// <summary>
        /// Integration reservation id stored in DB: <see cref="Reservation.ZaaerId"/> when set, otherwise internal reservation PK (aligned with <c>reservation_units.reservation_id</c>).
        /// </summary>
        [Column("reservation_id")]
        [Required]
        public int ReservationId { get; set; }

        [Column("unit_id")]
        [Required]
        public int UnitId { get; set; }

        [Column("from_apartment_id")]
        [Required]
        public int FromApartmentId { get; set; }

        [Column("to_apartment_id")]
        [Required]
        public int ToApartmentId { get; set; }

        [Column("apply_mode")]
        [MaxLength(30)]
        public string ApplyMode { get; set; } = "SamePrice"; // SamePrice | NewFromToday | NewForAllDays

        [Column("effective_date")]
        public DateTime? EffectiveDate { get; set; }

        [Column("comment")]
        [MaxLength(500)]
        public string? Comment { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        /// <summary>
        /// Master user id (from central auth) who performed the switch, when known.
        /// </summary>
        [Column("created_by_user_id")]
        public int? CreatedByUserId { get; set; }

        /// <summary>
        /// Zaaer System ID (معرف Zaaer)
        /// External ID from Zaaer integration system
        /// </summary>
        [Column("zaaer_id")]
        public int? ZaaerId { get; set; }

        [ForeignKey("UnitId")]
        public ReservationUnit ReservationUnit { get; set; } = null!;
    }
}


