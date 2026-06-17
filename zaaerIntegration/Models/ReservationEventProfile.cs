using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FinanceLedgerAPI.Enums;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    [Table("reservation_event_profiles")]
    public class ReservationEventProfile
    {
        [Key]
        [Column("event_profile_id")]
        public int EventProfileId { get; set; }

        [Column("hotel_id")]
        public int HotelId { get; set; }

        [Column("reservation_id")]
        public int ReservationId { get; set; }

        [Column("hall_id")]
        public int? HallId { get; set; }

        [Column("event_type")]
        [MaxLength(50)]
        public string EventType { get; set; } = HallEventTypes.Wedding;

        [Column("event_date", TypeName = "date")]
        public DateTime EventDate { get; set; }

        [Column("event_date_hijri")]
        [MaxLength(20)]
        public string? EventDateHijri { get; set; }

        [Column("event_start_time", TypeName = "time")]
        public TimeSpan EventStartTime { get; set; }

        [Column("event_end_time", TypeName = "time")]
        public TimeSpan EventEndTime { get; set; }

        [Column("expected_guests")]
        public int ExpectedGuests { get; set; }

        [Column("actual_guests")]
        public int? ActualGuests { get; set; }

        [Column("occasion_name")]
        [MaxLength(300)]
        public string? OccasionName { get; set; }

        [Column("occasion_owner")]
        [MaxLength(300)]
        public string? OccasionOwner { get; set; }

        [Column("deposit_amount", TypeName = "decimal(12,2)")]
        public decimal DepositAmount { get; set; }

        [Column("remaining_balance", TypeName = "decimal(12,2)")]
        public decimal RemainingBalance { get; set; }

        [Column("contract_signed")]
        public bool ContractSigned { get; set; }

        [Column("contract_signed_at")]
        public DateTime? ContractSignedAt { get; set; }

        [Column("event_status")]
        [MaxLength(50)]
        public string EventStatus { get; set; } = HallEventStatusCodes.Unconfirmed;

        [Column("completion_notes")]
        public string? CompletionNotes { get; set; }

        [Column("deposit_due_at")]
        public DateTime? DepositDueAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [ForeignKey(nameof(ReservationId))]
        public Reservation? Reservation { get; set; }
    }
}
