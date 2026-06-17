using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Secondary guests (companions) linked to a reservation — table <c>reservation_companions</c>.
    /// </summary>
    [Table("reservation_companions")]
    public class ReservationCompanion
    {
        [Key]
        [Column("companion_id")]
        public int CompanionId { get; set; }

        /// <summary>
        /// Zaaer reservation id when <c>reservations.zaaer_id</c> is set; otherwise internal <c>reservation_id</c> (drafts).
        /// </summary>
        [Column("reservation_id")]
        public int ReservationId { get; set; }

        /// <summary>
        /// Zaaer customer id when set on the guest row; otherwise internal <c>customers.customer_id</c>.
        /// </summary>
        [Column("customer_id")]
        public int CustomerId { get; set; }

        /// <summary>
        /// Apartment zaaer id when set; otherwise internal <c>apartments.apartment_id</c> for the companion unit line.
        /// </summary>
        [Column("unit_id")]
        public int? UnitId { get; set; }

        /// <summary>Optional internal <c>apartments.apartment_id</c> (denormalized for reporting).</summary>
        [Column("apartment_id")]
        public int? ApartmentId { get; set; }

        /// <summary>FK to <c>customer_relations.cr_id</c>.</summary>
        [Column("relation_id")]
        public int? RelationId { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;
    }
}
