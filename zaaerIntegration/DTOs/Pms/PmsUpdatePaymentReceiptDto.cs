using System.ComponentModel.DataAnnotations;

namespace zaaerIntegration.DTOs.Pms
{
  public sealed class PmsUpdatePaymentReceiptDto
  {
    [Required]
    public int HotelId { get; set; }

    [Required]
    public int ReservationId { get; set; }

    public int? CustomerId { get; set; }

    public int? UnitId { get; set; }

    [Required]
    [StringLength(50)]
    public string ReceiptType { get; set; } = "receipt";

    [StringLength(50)]
    public string? VoucherCode { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal AmountPaid { get; set; }

    public DateTime? ReceiptDate { get; set; }

    public int? PaymentMethodId { get; set; }

    public int? BankId { get; set; }

    [StringLength(100)]
    public string? TransactionNo { get; set; }

    public DateTime? ReceiptFrom { get; set; }

    public DateTime? ReceiptTo { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }

    /// <summary>Rent receipt only — building guard rent (requires <c>payments.building_guard_rent</c>).</summary>
    public bool IsBuildingGuardRent { get; set; }
  }
}
