using System.ComponentModel.DataAnnotations;

namespace zaaerIntegration.DTOs.Pms.ReservationDetail
{
    public sealed class ReservationHallRentUpdateDto
    {
        [Required]
        [Range(0, 999999999.99)]
        public decimal HallRentAmount { get; set; }
    }
}
