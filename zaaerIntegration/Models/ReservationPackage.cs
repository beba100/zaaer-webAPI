using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Reservation package / add-on catalog used when adding reservation extras.
    /// </summary>
    [Table("packages")]
    public class ReservationPackage
    {
        [Key]
        [Column("package_id")]
        public int PackageId { get; set; }

        [Column("hotel_id")]
        public int? HotelId { get; set; }

        [Required]
        [MaxLength(400)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(400)]
        [Column("name_ar")]
        public string? NameAr { get; set; }

        [MaxLength(1000)]
        [Column("description")]
        public string? Description { get; set; }

        [Column("unit_price", TypeName = "decimal(12,2)")]
        public decimal UnitPrice { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("sort_order")]
        public int SortOrder { get; set; } = 100;

        [Column("price_type")]
        [MaxLength(30)]
        public string PriceType { get; set; } = "fixed";

        [Column("package_category")]
        [MaxLength(50)]
        public string? PackageCategory { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
