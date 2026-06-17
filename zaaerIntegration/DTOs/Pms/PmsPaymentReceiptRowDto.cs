namespace zaaerIntegration.DTOs.Pms
{
  /// <summary>
  /// Payment receipt row for reservation-detail payments grids.
  /// </summary>
  public sealed class PmsPaymentReceiptRowDto
  {
    /// <summary>Internal PK (<c>receipt_id</c>) — grid/DevExtreme key only.</summary>
    public int ReceiptId { get; set; }

    /// <summary>Unique integration id (<c>zaaer_id</c>) — PMS update API route key.</summary>
    public int? ZaaerId { get; set; }
    public string ReceiptNo { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; }
    public decimal AmountPaid { get; set; }
    public string? PaymentMethod { get; set; }
    public int? PaymentMethodId { get; set; }
    public string? VoucherCode { get; set; }
    public string ReceiptStatus { get; set; } = "active";
    public DateTime? ReceiptFrom { get; set; }
    public DateTime? ReceiptTo { get; set; }
    public string ReceiptType { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? Reason { get; set; }
    public int? BankId { get; set; }
    public string? TransactionNo { get; set; }
    public int HotelId { get; set; }
    public int? ReservationId { get; set; }
    public int? CustomerId { get; set; }
    public int? UnitId { get; set; }
    public int? OrderId { get; set; }
    public bool IsBuildingGuardRent { get; set; }

    // Legacy aliases for older clients
    public string Number => ReceiptNo;
    public DateTime Date => ReceiptDate;
    public decimal Amount => AmountPaid;
    public string Status => ReceiptStatus;
    public string? PaymentMethodName => PaymentMethod;
  }

  /// <summary>Last rent receipt period hint for PMS receipt popup (from DB columns).</summary>
  public sealed class PmsLastRentReceiptDto
  {
    public string ReceiptNo { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; }
    public DateTime ReceiptFrom { get; set; }
    public DateTime ReceiptTo { get; set; }
  }
}
