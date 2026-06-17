using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    [Table("booking_engine_media")]
    public class BookingEngineMedia
    {
        [Key]
        [Column("media_id")]
        public int MediaId { get; set; }

        [Column("hotel_id")]
        public int HotelId { get; set; }

        /// <summary>When set, image is for a room type; otherwise hotel-level gallery.</summary>
        [Column("room_type_id")]
        public int? RoomTypeId { get; set; }

        [Column("image_url")]
        [MaxLength(500)]
        public string ImageUrl { get; set; } = string.Empty;

        [Column("caption")]
        [MaxLength(300)]
        public string? Caption { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; }

        [Column("is_primary")]
        public bool IsPrimary { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;
    }
}
