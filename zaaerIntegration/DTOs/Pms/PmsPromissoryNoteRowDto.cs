namespace zaaerIntegration.DTOs.Pms
{
    public sealed class PmsPromissoryNoteRowDto
    {
        public int PromissoryNoteId { get; set; }
        public int? ZaaerId { get; set; }
        public string PromissoryNo { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime MaturityDate { get; set; }
        public decimal Amount { get; set; }
        public decimal AmountCollected { get; set; }
        public decimal DueAmount { get; set; }
        public string Status { get; set; } = "open";
        public string? PayableTo { get; set; }
        public string? Reason { get; set; }
        public string? PlaceOfMaturity { get; set; }
        public string? Notes { get; set; }
        public bool PaymentLinkSent { get; set; }
        public int? CollectionReceiptId { get; set; }
        public string? CollectionReceiptNo { get; set; }
        public int HotelId { get; set; }
        public int? ReservationId { get; set; }
        public int? CustomerId { get; set; }
        public int? CorporateId { get; set; }

        // Grid aliases
        public int Id => PromissoryNoteId;
        public string Number => PromissoryNo;
        public DateTime Date => CreatedAt;
    }
}
