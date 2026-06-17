using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    [Table("event_function_sheets")]
    public class EventFunctionSheet
    {
        [Key]
        [Column("function_sheet_id")]
        public int FunctionSheetId { get; set; }

        [Column("hotel_id")]
        public int HotelId { get; set; }

        [Column("reservation_id")]
        public int ReservationId { get; set; }

        [Column("hall_id")]
        public int? HallId { get; set; }

        [Column("event_date", TypeName = "date")]
        public DateTime EventDate { get; set; }

        [Column("event_date_hijri")]
        [MaxLength(20)]
        public string? EventDateHijri { get; set; }

        [Column("hall_open_time", TypeName = "time")]
        public TimeSpan? HallOpenTime { get; set; }

        [Column("guest_arrival_time", TypeName = "time")]
        public TimeSpan? GuestArrivalTime { get; set; }

        [Column("service_start_time", TypeName = "time")]
        public TimeSpan? ServiceStartTime { get; set; }

        [Column("coffee_type")]
        [MaxLength(200)]
        public string? CoffeeType { get; set; }

        [Column("menu_notes")]
        public string? MenuNotes { get; set; }

        [Column("decoration_notes")]
        public string? DecorationNotes { get; set; }

        [Column("sound_av_notes")]
        public string? SoundAvNotes { get; set; }

        [Column("coordinator_user_id")]
        public int? CoordinatorUserId { get; set; }

        [Column("client_special_requests")]
        public string? ClientSpecialRequests { get; set; }

        [Column("execution_status")]
        [MaxLength(30)]
        public string ExecutionStatus { get; set; } = "draft";

        [Column("printed_at")]
        public DateTime? PrintedAt { get; set; }

        [Column("approved_by")]
        public int? ApprovedBy { get; set; }

        [Column("approved_at")]
        public DateTime? ApprovedAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        public ICollection<EventFunctionSheetItem> Items { get; set; } = new List<EventFunctionSheetItem>();
    }

    [Table("event_function_sheet_items")]
    public class EventFunctionSheetItem
    {
        [Key]
        [Column("item_id")]
        public int ItemId { get; set; }

        [Column("function_sheet_id")]
        public int FunctionSheetId { get; set; }

        [Column("category")]
        [MaxLength(50)]
        public string? Category { get; set; }

        [Column("item_name")]
        [MaxLength(300)]
        public string ItemName { get; set; } = string.Empty;

        [Column("quantity")]
        public decimal Quantity { get; set; } = 1;

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; }

        [ForeignKey(nameof(FunctionSheetId))]
        public EventFunctionSheet? FunctionSheet { get; set; }
    }
}
