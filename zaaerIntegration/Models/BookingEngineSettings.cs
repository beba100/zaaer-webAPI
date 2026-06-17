using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Per-hotel public booking engine configuration.
    /// </summary>
    [Table("booking_engine_settings")]
    public class BookingEngineSettings
    {
        [Key]
        [Column("settings_id")]
        public int SettingsId { get; set; }

        [Column("hotel_id")]
        public int HotelId { get; set; }

        [Column("is_enabled")]
        public bool IsEnabled { get; set; } = true;

        /// <summary>URL slug segment, e.g. jizan3 (optional; falls back to hotel_code).</summary>
        [Column("public_slug")]
        [MaxLength(80)]
        public string? PublicSlug { get; set; }

        [Column("logo_url")]
        [MaxLength(500)]
        public string? LogoUrl { get; set; }

        [Column("favicon_url")]
        [MaxLength(500)]
        public string? FaviconUrl { get; set; }

        [Column("banner_url")]
        [MaxLength(500)]
        public string? BannerUrl { get; set; }

        [Column("show_hotel_picker")]
        public bool ShowHotelPicker { get; set; }

        [Column("show_current_branch_only")]
        public bool ShowCurrentBranchOnly { get; set; } = true;

        [Column("minimum_stay_nights")]
        public int MinimumStayNights { get; set; } = 1;

        [Column("button_color")]
        [MaxLength(20)]
        public string? ButtonColor { get; set; }

        [Column("border_color")]
        [MaxLength(20)]
        public string? BorderColor { get; set; }

        [Column("background_color")]
        [MaxLength(20)]
        public string? BackgroundColor { get; set; }

        [Column("top_filter_html")]
        public string? TopFilterHtml { get; set; }

        [Column("down_filter_html")]
        public string? DownFilterHtml { get; set; }

        [Column("contact_email")]
        [MaxLength(200)]
        public string? ContactEmail { get; set; }

        [Column("contact_phone")]
        [MaxLength(50)]
        public string? ContactPhone { get; set; }

        [Column("contact_description")]
        [MaxLength(2000)]
        public string? ContactDescription { get; set; }

        /// <summary>none | optional | required</summary>
        [Column("deposit_mode")]
        [MaxLength(20)]
        public string DepositMode { get; set; } = "optional";

        [Column("deposit_amount", TypeName = "decimal(12,2)")]
        public decimal? DepositAmount { get; set; }

        [Column("deposit_percent", TypeName = "decimal(5,2)")]
        public decimal? DepositPercent { get; set; }

        [Column("online_deposit_enabled")]
        public bool OnlineDepositEnabled { get; set; }

        /// <summary>When true, public booking page shows closed message and blocks search/confirm.</summary>
        [Column("sales_closed")]
        public bool SalesClosed { get; set; }

        [Column("sales_closed_message")]
        [MaxLength(1000)]
        public string? SalesClosedMessage { get; set; }

        /// <summary>both | daily_only | monthly_only | hidden — public search rental dropdown.</summary>
        [Column("rental_type_mode")]
        [MaxLength(20)]
        public string RentalTypeMode { get; set; } = "both";

        [Column("promo_banner_enabled")]
        public bool PromoBannerEnabled { get; set; }

        [Column("promo_banner_image_url")]
        [MaxLength(500)]
        public string? PromoBannerImageUrl { get; set; }

        [Column("promo_banner_html")]
        public string? PromoBannerHtml { get; set; }

        [Column("promo_banner_ends_at")]
        public DateTime? PromoBannerEndsAt { get; set; }

        /// <summary>actual | override | min — how website availability relates to PMS actual count.</summary>
        [Column("availability_mode")]
        [MaxLength(20)]
        public string AvailabilityMode { get; set; } = "actual";

        /// <summary>standard | programmatic — last-resort gross when no rate tables.</summary>
        [Column("rate_fallback_mode")]
        [MaxLength(20)]
        public string RateFallbackMode { get; set; } = "standard";

        [Column("rate_fallback_min", TypeName = "decimal(12,2)")]
        public decimal? RateFallbackMin { get; set; }

        [Column("rate_fallback_max", TypeName = "decimal(12,2)")]
        public decimal? RateFallbackMax { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
