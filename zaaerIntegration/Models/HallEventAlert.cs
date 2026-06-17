using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    [Table("hall_event_alerts")]
    public class HallEventAlert
    {
        [Key]
        [Column("alert_id")]
        public int AlertId { get; set; }

        [Column("hotel_id")]
        public int HotelId { get; set; }

        [Column("reservation_id")]
        public int ReservationId { get; set; }

        [Column("alert_type")]
        [MaxLength(50)]
        public string AlertType { get; set; } = string.Empty;

        [Column("message")]
        [MaxLength(500)]
        public string Message { get; set; } = string.Empty;

        [Column("severity")]
        [MaxLength(20)]
        public string Severity { get; set; } = "info";

        [Column("is_read")]
        public bool IsRead { get; set; }

        [Column("due_at")]
        public DateTime? DueAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;
    }
}
