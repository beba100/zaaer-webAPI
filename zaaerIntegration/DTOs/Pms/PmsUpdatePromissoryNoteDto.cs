using System.ComponentModel.DataAnnotations;

namespace zaaerIntegration.DTOs.Pms
{
    public sealed class PmsUpdatePromissoryNoteDto
    {
        [Required]
        public int HotelId { get; set; }

        [Required]
        public int ReservationId { get; set; }

        [StringLength(500)]
        public string? Reason { get; set; }

        [StringLength(200)]
        public string? PlaceOfMaturity { get; set; }

        public DateTime? MaturityDate { get; set; }

        [Range(0.01, double.MaxValue)]
        public decimal? Amount { get; set; }

        public bool? PaymentLinkSent { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }
    }
}
