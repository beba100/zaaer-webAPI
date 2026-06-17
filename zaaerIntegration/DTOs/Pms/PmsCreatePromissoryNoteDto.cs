using System.ComponentModel.DataAnnotations;

namespace zaaerIntegration.DTOs.Pms
{
    public sealed class PmsCreatePromissoryNoteDto
    {
        [Required]
        public int HotelId { get; set; }

        /// <summary>Internal <c>reservations.reservation_id</c>.</summary>
        [Required]
        public int ReservationId { get; set; }

        public int? CustomerId { get; set; }

        public int? CorporateId { get; set; }

        [StringLength(200)]
        public string? PayableTo { get; set; }

        [StringLength(500)]
        public string? Reason { get; set; }

        [StringLength(200)]
        public string? PlaceOfMaturity { get; set; }

        [Required]
        public DateTime MaturityDate { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }

        public bool PaymentLinkSent { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public int? CreatedBy { get; set; }
    }
}
