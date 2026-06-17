#pragma warning disable CS1591

using System.ComponentModel.DataAnnotations;

namespace zaaerIntegration.DTOs.Pms.ReservationDetail
{
    /// <summary>
    /// PMS body to move a checked-in reservation line to another apartment.
    /// <see cref="ToApartmentId"/> may be internal <c>apartment_id</c> or <c>zaaer_id</c> as returned by for-picker.
    /// </summary>
    public sealed class ReservationUnitSwapRequestDto
    {
        [Required]
        public int UnitId { get; set; }

        [Required]
        public int ToApartmentId { get; set; }

        [Required]
        [MaxLength(30)]
        public string ApplyMode { get; set; } = "SamePrice";

        public DateTime? EffectiveDate { get; set; }

        [MaxLength(500)]
        public string? Comment { get; set; }
    }
}
