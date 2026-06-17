using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    [Table("resort_ticket_types")]
    public class ResortTicketType
    {
        [Key]
        [Column("ticket_type_id")]
        public int TicketTypeId { get; set; }

        [Column("hotel_id")]
        public int HotelId { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        [Column("name_ar")]
        public string NameAr { get; set; } = string.Empty;

        [MaxLength(200)]
        [Column("name_en")]
        public string? NameEn { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("unit_price", TypeName = "decimal(12,2)")]
        public decimal UnitPrice { get; set; }

        [Column("vat_rate", TypeName = "decimal(5,2)")]
        public decimal VatRate { get; set; }

        [Column("valid_for_hours")]
        public int ValidForHours { get; set; } = 24;

        /// <summary>Play/session duration in minutes (preferred over valid_for_hours when &gt; 0).</summary>
        [Column("valid_for_minutes")]
        public int ValidForMinutes { get; set; }

        /// <summary>business_day = validity from issue; from_first_scan = timer starts at first gate scan.</summary>
        [MaxLength(30)]
        [Column("validity_mode")]
        public string ValidityMode { get; set; } = ResortTicketValidityModes.BusinessDay;

        [MaxLength(50)]
        [Column("ticket_category")]
        public string TicketCategory { get; set; } = ResortTicketCategories.Other;

        [Column("sort_order")]
        public int SortOrder { get; set; }

        [Column("is_generic")]
        public bool IsGeneric { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("zaaer_id")]
        public int? ZaaerId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }

    [Table("resort_ticket_orders")]
    public class ResortTicketOrder
    {
        [Key]
        [Column("ticket_order_id")]
        public int TicketOrderId { get; set; }

        [Column("hotel_id")]
        public int HotelId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("order_no")]
        public string OrderNo { get; set; } = string.Empty;

        [Column("reservation_id")]
        public int? ReservationId { get; set; }

        [Column("unit_id")]
        public int? UnitId { get; set; }

        [Column("customer_id")]
        public int? CustomerId { get; set; }

        [Column("invoice_id")]
        public int? InvoiceId { get; set; }

        [Column("receipt_id")]
        public int? ReceiptId { get; set; }

        [Column("refund_receipt_id")]
        public int? RefundReceiptId { get; set; }

        [Column("order_date")]
        public DateTime OrderDate { get; set; } = KsaTime.Now;

        [Column("service_date", TypeName = "date")]
        public DateTime ServiceDate { get; set; } = KsaTime.Now.Date;

        [Column("subtotal", TypeName = "decimal(12,2)")]
        public decimal Subtotal { get; set; }

        [Column("vat_amount", TypeName = "decimal(12,2)")]
        public decimal VatAmount { get; set; }

        [Column("total_amount", TypeName = "decimal(12,2)")]
        public decimal TotalAmount { get; set; }

        [MaxLength(30)]
        [Column("payment_status")]
        public string PaymentStatus { get; set; } = "unpaid";

        [MaxLength(30)]
        [Column("order_status")]
        public string OrderStatus { get; set; } = "active";

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("cancelled_by")]
        public int? CancelledBy { get; set; }

        [Column("cancelled_at")]
        public DateTime? CancelledAt { get; set; }

        [Column("cancel_reason")]
        public string? CancelReason { get; set; }

        [Column("zaaer_id")]
        public int? ZaaerId { get; set; }
    }

    [Table("resort_tickets")]
    public class ResortTicket
    {
        [Key]
        [Column("ticket_id")]
        public int TicketId { get; set; }

        [Column("hotel_id")]
        public int HotelId { get; set; }

        [Column("ticket_order_id")]
        public int TicketOrderId { get; set; }

        [Column("ticket_type_id")]
        public int TicketTypeId { get; set; }

        [Required]
        [MaxLength(120)]
        [Column("ticket_no")]
        public string TicketNo { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        [Column("qr_code")]
        public string QrCode { get; set; } = string.Empty;

        [MaxLength(30)]
        [Column("ticket_status")]
        public string TicketStatus { get; set; } = "issued";

        [Column("unit_price", TypeName = "decimal(12,2)")]
        public decimal UnitPrice { get; set; }

        [Column("vat_amount", TypeName = "decimal(12,2)")]
        public decimal VatAmount { get; set; }

        [Column("total_amount", TypeName = "decimal(12,2)")]
        public decimal TotalAmount { get; set; }

        [Column("valid_from")]
        public DateTime ValidFrom { get; set; }

        [Column("valid_to")]
        public DateTime ValidTo { get; set; }

        [Column("printed_at")]
        public DateTime? PrintedAt { get; set; }

        [Column("used_at")]
        public DateTime? UsedAt { get; set; }

        /// <summary>When a from_first_scan ticket session was activated (first redeem at attraction).</summary>
        [Column("session_started_at")]
        public DateTime? SessionStartedAt { get; set; }

        [Column("cancelled_at")]
        public DateTime? CancelledAt { get; set; }

        [Column("cancelled_by")]
        public int? CancelledBy { get; set; }

        [Column("cancel_reason")]
        public string? CancelReason { get; set; }

        [Column("zaaer_id")]
        public int? ZaaerId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;
    }

    [Table("resort_ticket_config")]
    public class ResortTicketConfig
    {
        [Key]
        [Column("hotel_id")]
        public int HotelId { get; set; }

        /// <summary>Earliest KSA time employees may issue tickets for the current business day (e.g. 16:00).</summary>
        [Column("issue_start_time")]
        public TimeSpan IssueStartTime { get; set; } = new TimeSpan(16, 0, 0);

        /// <summary>End of validity for general tickets on the business day (e.g. 04:00 next calendar day).</summary>
        [Column("ticket_validity_end_time")]
        public TimeSpan TicketValidityEndTime { get; set; } = new TimeSpan(4, 0, 0);

        /// <summary>Optional override for games-category tickets.</summary>
        [Column("games_validity_end_time")]
        public TimeSpan? GamesValidityEndTime { get; set; }

        /// <summary>Business day closes at this KSA time (e.g. 04:00).</summary>
        [Column("daily_close_time")]
        public TimeSpan DailyCloseTime { get; set; } = new TimeSpan(4, 0, 0);

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }

    [Table("resort_ticket_events")]
    public class ResortTicketEvent
    {
        [Key]
        [Column("event_id")]
        public int EventId { get; set; }

        [Column("hotel_id")]
        public int HotelId { get; set; }

        [Column("ticket_id")]
        public int? TicketId { get; set; }

        [Column("ticket_order_id")]
        public int? TicketOrderId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("event_type")]
        public string EventType { get; set; } = string.Empty;

        [Column("event_note")]
        public string? EventNote { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;
    }
}
