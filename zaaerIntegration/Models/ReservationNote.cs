using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Reservation notes thread — multiple entries per reservation (internal / guest-facing).
    /// <see cref="ReservationId"/> stores the global integration id:
    /// <c>reservations.zaaer_id</c> when &gt; 0, otherwise <c>reservations.reservation_id</c>.
    /// </summary>
    [Table("reservation_notes")]
    public class ReservationNote
    {
        [Key]
        [Column("note_id")]
        public int NoteId { get; set; }

        [Required]
        [Column("hotel_id")]
        public int HotelId { get; set; }

        [Required]
        [Column("reservation_id")]
        public int ReservationId { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("note_type")]
        public string NoteType { get; set; } = ReservationNoteTypes.Internal;

        [Required]
        [MaxLength(2000)]
        [Column("note_text")]
        public string NoteText { get; set; } = string.Empty;

        [MaxLength(500)]
        [Column("attachment_path")]
        public string? AttachmentPath { get; set; }

        [MaxLength(255)]
        [Column("attachment_original_name")]
        public string? AttachmentOriginalName { get; set; }

        [MaxLength(100)]
        [Column("attachment_content_type")]
        public string? AttachmentContentType { get; set; }

        [Column("attachment_file_size")]
        public long? AttachmentFileSize { get; set; }

        /// <summary>PMS operator id (<c>pms_users.user_id</c> from JWT).</summary>
        [Column("created_by_user_id")]
        public int? CreatedByUserId { get; set; }

        [MaxLength(200)]
        [Column("created_by_name")]
        public string? CreatedByName { get; set; }

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }

    public static class ReservationNoteTypes
    {
        public const string Internal = "internal";
        public const string Guest = "guest";
    }
}
