using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Per-room color overrides for occupied room cards on the PMS room board.
    /// The link is by <c>apartment_zaaer_id</c> to <c>apartments.zaaer_id</c> (or the apartment internal id when no Zaaer id exists).
    /// </summary>
    [Table("room_card_color_settings")]
    public class RoomCardColorSetting
    {
        [Key]
        [Column("setting_id")]
        public int SettingId { get; set; }

        [Column("hotel_id")]
        public int HotelId { get; set; }

        [Column("apartment_zaaer_id")]
        public int ApartmentZaaerId { get; set; }

        [StringLength(40)]
        [Column("occupied_card_back_color")]
        public string? OccupiedCardBackColor { get; set; }

        [StringLength(40)]
        [Column("occupied_header_back_color")]
        public string? OccupiedHeaderBackColor { get; set; }

        [StringLength(40)]
        [Column("occupied_guest_back_color")]
        public string? OccupiedGuestBackColor { get; set; }

        [StringLength(40)]
        [Column("occupied_dates_back_color")]
        public string? OccupiedDatesBackColor { get; set; }

        [StringLength(40)]
        [Column("occupied_text_color")]
        public string? OccupiedTextColor { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
