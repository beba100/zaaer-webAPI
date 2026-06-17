using System.ComponentModel.DataAnnotations;

namespace zaaerIntegration.DTOs.Pms
{
    public sealed class PmsCancelPromissoryNoteDto
    {
        [Required]
        public int HotelId { get; set; }

        [Required]
        public int ReservationId { get; set; }

        [Required]
        [StringLength(500)]
        public string? Reason { get; set; }
    }
}
