using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    [Table("booking_engine_availability_override")]
    public class BookingEngineAvailabilityOverride
    {
        [Key]
        [Column("override_id")]
        public int OverrideId { get; set; }

        [Column("hotel_id")]
        public int HotelId { get; set; }

        [Column("roomtype_id")]
        public int RoomTypeId { get; set; }

        [Column("rate_date", TypeName = "date")]
        public DateTime RateDate { get; set; }

        [Column("display_units")]
        public int DisplayUnits { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
