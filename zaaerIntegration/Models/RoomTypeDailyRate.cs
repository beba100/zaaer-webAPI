using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Per-day gross rate override for a room type (PMS rates calendar).
    /// </summary>
    [Table("room_type_daily_rates")]
    public class RoomTypeDailyRate
    {
        [Key]
        [Column("daily_rate_id")]
        public int DailyRateId { get; set; }

        /// <summary>
        /// Zaaer hotel id (matches <see cref="HotelSettings.ZaaerId"/> / scope hotel id), not local hotel_settings PK.
        /// </summary>
        [Column("hotel_id")]
        [Required]
        public int HotelId { get; set; }

        /// <summary>
        /// Zaaer room type id (matches <see cref="RoomType.ZaaerId"/>), not internal room_types PK when Zaaer id exists.
        /// </summary>
        [Column("roomtype_id")]
        [Required]
        public int RoomTypeId { get; set; }

        [Column("rate_date", TypeName = "date")]
        public DateTime RateDate { get; set; }

        [Column("gross_rate", TypeName = "decimal(12,2)")]
        public decimal GrossRate { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
