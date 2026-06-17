using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Property facility (مرفق) — common areas linked to block/floor by zaaer_id.
    /// </summary>
    [Table("facilities")]
    public class Facility
    {
        [Key]
        [Column("facility_id")]
        public int FacilityId { get; set; }

        [Column("hotel_id")]
        [Required]
        public int HotelId { get; set; }

        [Column("zaaer_id")]
        public int? ZaaerId { get; set; }

        [Column("facility_name")]
        [Required]
        [MaxLength(200)]
        public string FacilityName { get; set; } = string.Empty;

        [Column("facility_name_en")]
        [MaxLength(200)]
        public string? FacilityNameEn { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        /// <summary>Buildings.zaaer_id when set.</summary>
        [Column("building_id")]
        public int? BuildingId { get; set; }

        /// <summary>Floors.zaaer_id when set.</summary>
        [Column("floor_id")]
        public int? FloorId { get; set; }

        [Column("image_urls_json")]
        public string? ImageUrlsJson { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [ForeignKey("HotelId")]
        public HotelSettings HotelSettings { get; set; } = null!;
    }
}
