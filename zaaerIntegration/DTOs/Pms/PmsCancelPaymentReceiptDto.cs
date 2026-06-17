using System.ComponentModel.DataAnnotations;

namespace zaaerIntegration.DTOs.Pms
{
    /// <summary>Cancel (void) a payment receipt by integration id.</summary>
    public sealed class PmsCancelPaymentReceiptDto
    {
        [Required]
        public int HotelId { get; set; }

        /// <summary>Internal <c>reservations.reservation_id</c> (validation scope).</summary>
        [Required]
        public int ReservationId { get; set; }

        [Required]
        [StringLength(500)]
        public string Reason { get; set; } = string.Empty;
    }
}
