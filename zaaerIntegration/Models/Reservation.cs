using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FinanceLedgerAPI.Enums;

namespace FinanceLedgerAPI.Models
{
	/// <summary>
	/// جدول الحجز الرئيسي - Main reservations table
	/// </summary>
	[Table("reservations")]
	public class Reservation
	{
		[Key]
		[Column("reservation_id")]
		public int ReservationId { get; set; }

		[Column("reservation_no")]
		[Required]
		[MaxLength(50)]
		public string ReservationNo { get; set; }

		[Column("hotel_id")]
		[Required]
		public int HotelId { get; set; }

		[Column("customer_id")]
		public int? CustomerId { get; set; }

	[Column("visit_purpose_id")]
	public int? VisitPurposeId { get; set; }

	[Column("corporate_id")]
	public int? CorporateId { get; set; }

	/// <summary>
	/// Reservation Type: Individual or Corporate
	/// نوع الحجز: فردي أو شركة
	/// </summary>
	[Column("reservation_type")]
	[MaxLength(20)]
	public string ReservationType { get; set; } = "Individual";

		[Column("reservation_date")]
		public DateTime ReservationDate { get; set; } = DateTime.Now;

		/// <summary>
		/// Rental type influences pricing calculation
		/// </summary>
		[Column("rental_type")]
		[MaxLength(20)]
		public string RentalType { get; set; } = FinanceLedgerAPI.Enums.RentalType.Daily.ToString();

	/// <summary>
	/// Number of months for monthly rental type (عدد الشهور للحجز الشهري)
	/// </summary>
	[Column("number_of_months")]
	public int? NumberOfMonths { get; set; }

	/// <summary>
	/// Monthly rental calendar mode: ThirtyDay (30-day blocks) or Actual (Gregorian months).
	/// </summary>
	[Column("monthly_calendar_mode")]
	[MaxLength(20)]
	public string? MonthlyCalendarMode { get; set; }

	[Column("total_nights")]
	public int? TotalNights { get; set; }

	/// <summary>
	/// Total penalties added to reservation (يزيد الإجمالي)
	/// </summary>
	[Column("total_penalties", TypeName = "decimal(12,2)")]
	public decimal? TotalPenalties { get; set; }

	/// <summary>
	/// Total discounts subtracted from reservation (يقلل الإجمالي)
	/// </summary>
	[Column("total_discounts", TypeName = "decimal(12,2)")]
	public decimal? TotalDiscounts { get; set; }

	[Column("booking_coupon_id")]
	public int? BookingCouponId { get; set; }

	[Column("coupon_promo_code")]
	[StringLength(40)]
	public string? CouponPromoCode { get; set; }

	/// <summary>
	/// Subtotal before taxes (المجموع قبل الضرائب)
	/// </summary>
	[Column("subtotal", TypeName = "decimal(12,2)")]
	public decimal? Subtotal { get; set; }

	/// <summary>
	/// VAT Rate Applied (نسبة ضريبة القيمة المضافة)
	/// </summary>
	[Column("vat_rate", TypeName = "decimal(5,2)")]
	public decimal? VatRate { get; set; }

	/// <summary>
	/// VAT Amount (مبلغ ضريبة القيمة المضافة)
	/// </summary>
	[Column("vat_amount", TypeName = "decimal(12,2)")]
	public decimal? VatAmount { get; set; }

	/// <summary>
	/// Lodging Tax Rate Applied (نسبة ضريبة الإقامة)
	/// </summary>
	[Column("lodging_tax_rate", TypeName = "decimal(5,2)")]
	public decimal? LodgingTaxRate { get; set; }

	/// <summary>
	/// Lodging Tax Amount (مبلغ ضريبة الإقامة)
	/// </summary>
	[Column("lodging_tax_amount", TypeName = "decimal(12,2)")]
	public decimal? LodgingTaxAmount { get; set; }

	/// <summary>
	/// Total Tax Amount (إجمالي الضرائب)
	/// </summary>
	[Column("total_tax_amount", TypeName = "decimal(12,2)")]
	public decimal? TotalTaxAmount { get; set; }

	/// <summary>
	/// Total extra charges (إجمالي الإضافات)
	/// </summary>
	[Column("total_extra", TypeName = "decimal(18,2)")]
	public decimal? TotalExtra { get; set; }

	[Column("total_amount", TypeName = "decimal(12,2)")]
	public decimal? TotalAmount { get; set; }

	[Column("amount_paid", TypeName = "decimal(12,2)")]
	public decimal? AmountPaid { get; set; }

	[Column("balance_amount", TypeName = "decimal(12,2)")]
	public decimal? BalanceAmount { get; set; }

	[Column("check_in_date")]
	public DateTime? CheckInDate { get; set; }

	[Column("check_out_date")]
	public DateTime? CheckOutDate { get; set; }

	[Column("departure_date")]
	public DateTime? DepartureDate { get; set; }

		[Column("status")]
		[MaxLength(50)]
		public string Status { get; set; } = "Unconfirmed";

		/// <summary>
		/// Reservation Status as Enum
		/// حالة الحجز كـ Enum
		/// </summary>
		[NotMapped]
		public ReservationStatus StatusEnum
		{
			get => Enum.TryParse<ReservationStatus>(Status, true, out var result) ? result : ReservationStatus.Unconfirmed;
			set => Status = value.ToString();
		}

		/// <summary>
		/// Status as English Word (for API responses)
		/// الحالة بالكلمة الإنجليزية (للاستجابات)
		/// </summary>
		[NotMapped]
		public string StatusWord => StatusEnum.ToString();

		/// <summary>
		/// Status Display Name
		/// اسم الحالة للعرض
		/// </summary>
		[NotMapped]
		public string StatusDisplayName => ReservationStatusHelper.GetDisplayName(StatusEnum);

		/// <summary>
		/// Status Display Name in Arabic
		/// اسم الحالة بالعربية
		/// </summary>
		[NotMapped]
		public string StatusDisplayNameAr => ReservationStatusHelper.GetDisplayNameAr(StatusEnum);

		/// <summary>
		/// Status Color for UI
		/// لون الحالة للواجهة
		/// </summary>
		[NotMapped]
		public string StatusColor => ReservationStatusHelper.GetStatusColor(StatusEnum);

		/// <summary>PMS operator id (<c>pms_users.user_id</c> from JWT).</summary>
		[Column("created_by")]
		public int? CreatedBy { get; set; }

	[Column("created_at")]
	public DateTime CreatedAt { get; set; } = DateTime.Now;

	/// <summary>
	/// Automatic extension enabled (تمديد تلقائي)
	/// When true, the reservation can be extended automatically by the partner system (Zaaer)
	/// </summary>
	[Column("is_auto_extend")]
	public bool? IsAutoExtend { get; set; }

	/// <summary>
	/// Price Type ID (معرف نوع السعر)
	/// The rate type ID sent by Zaaer
	/// </summary>
	[Column("price_type_id")]
	public int? PriceTypeId { get; set; }

	/// <summary>
	/// Zaaer System ID (معرف Zaaer)
	/// External ID from Zaaer integration system
	/// </summary>
	[Column("zaaer_id")]
	public int? ZaaerId { get; set; }

	/// <summary>
	/// External Reference Number (مرجع خارجي)
	/// External reference number from Zaaer integration system (usually same as ZaaerId)
	/// </summary>
	[Column("external_ref_no")]
	public int? ExternalRefNo { get; set; }

	/// <summary>
	/// CM Booking Number (رقم الحجز في نظام CM)
	/// Booking number from CM system
	/// </summary>
	[Column("cm_booking_no")]
	[MaxLength(100)]
	public string? CmBookingNo { get; set; }

	/// <summary>
	/// Source point/platform (نقطة المصدر)
	/// The source of the reservation (e.g., "المطار", "الموقع", etc.)
	/// </summary>
        [Column("source")]
        [MaxLength(255)]
        public string? Source { get; set; }

        [Column("ntmp_transaction_id")]
        [MaxLength(64)]
        public string? NtmpTransactionId { get; set; }

        [Column("ntmp_last_sync_at")]
        public DateTime? NtmpLastSyncAt { get; set; }

        [MaxLength(100)]
        [Column("ntmp_last_event_type")]
        public string? NtmpLastEventType { get; set; }

        [MaxLength(20)]
        [Column("ntmp_last_status")]
        public string? NtmpLastStatus { get; set; }

        /// <summary>Bit flags: 1=booking, 2=check-in, 4=check-out (NTMP synced stages).</summary>
        [Column("ntmp_synced_stages")]
        public int NtmpSyncedStages { get; set; }

        // Navigation properties
		public VisitPurpose? VisitPurpose { get; set; }
		public CorporateCustomer? CorporateCustomer { get; set; }
		[ForeignKey("HotelId")]
		public HotelSettings HotelSettings { get; set; } = null!;
		public ICollection<ReservationUnit> ReservationUnits { get; set; } = new List<ReservationUnit>();
		public ICollection<ReservationCompanion> ReservationCompanions { get; set; } = new List<ReservationCompanion>();
		public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
		public ICollection<PaymentReceipt> PaymentReceipts { get; set; } = new List<PaymentReceipt>();
		public ICollection<Refund> Refunds { get; set; } = new List<Refund>();
	}
}

