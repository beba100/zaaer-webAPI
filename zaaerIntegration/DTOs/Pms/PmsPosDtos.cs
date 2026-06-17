using System.ComponentModel.DataAnnotations;

namespace zaaerIntegration.DTOs.Pms
{
    public sealed class PmsOutletDto
    {
        public int OutletId { get; init; }
        public int HotelId { get; init; }
        public string OutletName { get; init; } = string.Empty;
        public string? OutletNameAr { get; init; }
        public string? Location { get; init; }
        public string? ImageUrl { get; init; }
        public string Status { get; init; } = "Open";
        public bool IsActive { get; init; } = true;
        public int ItemCount { get; init; }
        public int TableCount { get; init; }
    }

    public sealed class PmsUpsertOutletDto
    {
        [Required]
        [MaxLength(200)]
        public string OutletName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? OutletNameAr { get; set; }

        [MaxLength(500)]
        public string? Location { get; set; }

        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Open";

        public bool IsActive { get; set; } = true;
    }

    public sealed class PmsOutletCategoryDto
    {
        public int CategoryId { get; init; }
        public int HotelId { get; init; }
        public string CategoryName { get; init; } = string.Empty;
        public string? CategoryNameAr { get; init; }
        public string? Description { get; init; }
        public int SortOrder { get; init; }
        public bool IsActive { get; init; } = true;
        public int ItemCount { get; init; }
    }

    public sealed class PmsUpsertOutletCategoryDto
    {
        [Required]
        [MaxLength(200)]
        public string CategoryName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? CategoryNameAr { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public sealed class PmsOutletItemDto
    {
        public int ItemId { get; init; }
        public int HotelId { get; init; }
        public int? OutletId { get; init; }
        public string? OutletName { get; init; }
        public int? CategoryId { get; init; }
        public string? CategoryName { get; init; }
        public string? ItemCode { get; init; }
        public string ItemName { get; init; } = string.Empty;
        public string? ItemNameAr { get; init; }
        public string? Description { get; init; }
        public decimal Price { get; init; }
        public int? Quantity { get; init; }
        public string? ImageUrl { get; init; }
        public bool IncludesTax { get; init; }
        public bool IsActive { get; init; } = true;
    }

    public sealed class PmsUpsertOutletItemDto
    {
        public int? OutletId { get; set; }
        public int? CategoryId { get; set; }

        [MaxLength(50)]
        public string? ItemCode { get; set; }

        [Required]
        [MaxLength(200)]
        public string ItemName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? ItemNameAr { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Range(0, 999999999)]
        public decimal Price { get; set; }

        public int? Quantity { get; set; }

        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        public bool IncludesTax { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public sealed class PmsOutletTableDto
    {
        public int TableId { get; init; }
        public int HotelId { get; init; }
        public int? OutletId { get; init; }
        public string? OutletName { get; init; }
        public string TableName { get; init; } = string.Empty;
        public string? TableNameAr { get; init; }
        public string? Description { get; init; }
        public int? Capacity { get; init; }
        public string Status { get; init; } = "Available";
        public bool IsActive { get; init; } = true;
    }

    public sealed class PmsUpsertOutletTableDto
    {
        [Required]
        public int OutletId { get; set; }

        [Required]
        [MaxLength(200)]
        public string TableName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? TableNameAr { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public int? Capacity { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Available";

        public bool IsActive { get; set; } = true;
    }

    public sealed class PmsPosPricingTaxDto
    {
        public decimal VatRate { get; init; }
        public decimal EwaRate { get; init; }
        public bool VatTaxIncluded { get; init; } = true;
        public bool LodgingTaxIncluded { get; init; } = true;
    }

    public sealed class PmsPosCatalogDto
    {
        public PmsOutletDto Outlet { get; init; } = null!;
        public IReadOnlyList<PmsOutletCategoryDto> Categories { get; init; } = Array.Empty<PmsOutletCategoryDto>();
        public IReadOnlyList<PmsOutletItemDto> Items { get; init; } = Array.Empty<PmsOutletItemDto>();
        public IReadOnlyList<PmsOutletTableDto> Tables { get; init; } = Array.Empty<PmsOutletTableDto>();
        public PmsPosPricingTaxDto? PricingTax { get; init; }
    }

    public sealed class PmsPosOrderLineDto
    {
        public int? ItemId { get; set; }

        [Required]
        [MaxLength(200)]
        public string ItemName { get; set; } = string.Empty;

        [Range(0.01, 999999)]
        public decimal Quantity { get; set; } = 1;

        [Range(0, 999999999)]
        public decimal UnitPrice { get; set; }

        [Range(0, 999999999)]
        public decimal Discount { get; set; }

        public bool IncludesTax { get; set; }

        /// <summary>Gross line total persisted on <c>order_items.total_price</c> (read-only).</summary>
        public decimal? TotalLineGross { get; set; }
    }

    public sealed class PmsUpdateTransferredPosOrderDto
    {
        [MaxLength(1000)]
        public string? Notes { get; set; }

        [Range(0, 999999999)]
        public decimal DiscountAmount { get; set; }

        [Required]
        [MinLength(1)]
        public List<PmsPosOrderLineDto> Lines { get; set; } = new();
    }

    public sealed class PmsPosPaymentLineDto
    {
        [Required]
        public int PaymentMethodId { get; set; }

        [Range(0.01, 999999999)]
        public decimal Amount { get; set; }

        public int? BankId { get; set; }

        [MaxLength(100)]
        public string? TransactionNo { get; set; }
    }

    public sealed class PmsCreatePosOrderDto
    {
        [Required]
        public int OutletId { get; set; }

        public int? TableId { get; set; }

        [MaxLength(200)]
        public string? GuestName { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        [Range(0, 999999999)]
        public decimal DiscountAmount { get; set; }

        /// <summary>Walk-in cash/card settlement lines. When omitted, order stays unpaid.</summary>
        public List<PmsPosPaymentLineDto>? Payments { get; set; }

        [Required]
        [MinLength(1)]
        public List<PmsPosOrderLineDto> Lines { get; set; } = new();

        /// <summary>When set, charge this in-house reservation (checked-in) and create a folio receipt.</summary>
        public int? ReservationId { get; set; }
    }

    public sealed class PmsPosInHouseReservationDto
    {
        public int ReservationId { get; init; }

        public string ReservationNo { get; init; } = string.Empty;

        public int? CustomerId { get; init; }

        public string CustomerName { get; init; } = string.Empty;

        /// <summary>Comma-separated room / unit labels for picker display.</summary>
        public string RoomLabels { get; init; } = string.Empty;

        /// <summary>Single-line label: reservation no · rooms · guest name.</summary>
        public string DisplayLabel { get; init; } = string.Empty;
    }

    public sealed class PmsPosOrderDto
    {
        public int OrderId { get; init; }
        public string OrderNo { get; init; } = string.Empty;
        public int HotelId { get; init; }
        public int? OutletId { get; init; }
        public string? OutletName { get; init; }
        public int? ReservationId { get; init; }
        public string OrderStatus { get; init; } = string.Empty;
        public string PaymentStatus { get; init; } = string.Empty;
        public string? OrderType { get; init; }
        public decimal? Subtotal { get; init; }
        public decimal? TaxAmount { get; init; }
        public decimal? DiscountAmount { get; init; }
        public decimal? TotalAmount { get; init; }
        public decimal PaidAmount { get; init; }
        public decimal? Balance { get; init; }
        public DateTime? OrderDate { get; init; }
        public string? OrderTime { get; init; }
        public DateTime CreatedAt { get; init; }
        public int? CreatedBy { get; init; }
        public string? CreatedByName { get; init; }
        public int? ReceiptId { get; init; }
        public string? ReceiptNo { get; init; }
        public int? PaymentMethodId { get; init; }
        public string? PaymentMethod { get; init; }
        public IReadOnlyList<PmsPosOrderLineDto> Lines { get; init; } = Array.Empty<PmsPosOrderLineDto>();
    }

    public sealed class PmsPosOrderListItemDto
    {
        public int OrderId { get; init; }
        public string OrderNo { get; init; } = string.Empty;
        public int? ReservationId { get; init; }
        public string? ReservationNo { get; init; }
        public decimal DisplayAmount { get; init; }
        public decimal? TotalAmount { get; init; }
        public int? OutletId { get; init; }
        public string? OutletName { get; init; }
        public string? OutletNameAr { get; init; }
        public string? CreatedByName { get; init; }
        public string? CreatedByUsername { get; init; }
        public string? CreatedByFirstName { get; init; }
        public string? CreatedByLastName { get; init; }
        public string? PaymentMethodAr { get; init; }
        public int? ReceiptBankId { get; init; }
        public string? ReceiptTransactionNo { get; init; }
        public DateTime? OrderDate { get; init; }
        public string? OrderTime { get; init; }
        public DateTime CreatedAt { get; init; }
        public string OrderStatus { get; init; } = string.Empty;
        public string PaymentStatus { get; init; } = string.Empty;
        public int? ReceiptId { get; init; }
        public string? ReceiptNo { get; init; }
        public int? PaymentMethodId { get; init; }
        public string? PaymentMethod { get; init; }
        public bool CanEditReceipt { get; init; }
        public bool CanEditTransferred { get; init; }
        public bool CanCancel { get; init; }
        public string? ReservationStatus { get; init; }
    }

    public sealed class PmsUpdatePosOrderReceiptDto
    {
        [Required]
        public DateTime ReceiptDate { get; set; }

        [Required]
        public int PaymentMethodId { get; set; }

        public int? BankId { get; set; }

        [MaxLength(100)]
        public string? TransactionNo { get; set; }
    }
}
