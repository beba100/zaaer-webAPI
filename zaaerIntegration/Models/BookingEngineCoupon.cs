using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    [Table("booking_engine_coupons")]
    public class BookingEngineCoupon
    {
        [Key]
        [Column("coupon_id")]
        public int CouponId { get; set; }

        [Column("hotel_id")]
        public int HotelId { get; set; }

        /// <summary>Global display number from Master (CUP-0001).</summary>
        [Column("coupon_no")]
        [MaxLength(50)]
        public string CouponNo { get; set; } = string.Empty;

        [Column("promo_code")]
        [MaxLength(40)]
        public string PromoCode { get; set; } = string.Empty;

        [Column("title")]
        [MaxLength(200)]
        public string? Title { get; set; }

        /// <summary>percent | fixed</summary>
        [Column("discount_type")]
        [MaxLength(20)]
        public string DiscountType { get; set; } = "percent";

        [Column("discount_value", TypeName = "decimal(12,2)")]
        public decimal DiscountValue { get; set; }

        [Column("min_stay_nights")]
        public int? MinStayNights { get; set; }

        [Column("min_booking_amount", TypeName = "decimal(12,2)")]
        public decimal? MinBookingAmount { get; set; }

        [Column("max_redemptions")]
        public int? MaxRedemptions { get; set; }

        [Column("redemption_count")]
        public int RedemptionCount { get; set; }

        [Column("valid_from", TypeName = "date")]
        public DateTime? ValidFrom { get; set; }

        [Column("valid_to", TypeName = "date")]
        public DateTime? ValidTo { get; set; }

        /// <summary>Comma-separated room type zaaer ids; empty = all types.</summary>
        [Column("room_type_ids")]
        [MaxLength(500)]
        public string? RoomTypeIds { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("notes")]
        [MaxLength(1000)]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
