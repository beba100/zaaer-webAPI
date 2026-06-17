#pragma warning disable CS1591

using System.ComponentModel.DataAnnotations;

namespace zaaerIntegration.DTOs.RoomBoard
{
    public sealed class RoomBoardMaintenanceUpdateRequestDto
    {
        [Required]
        public string FromDate { get; set; } = string.Empty;

        [Required]
        public string ToDate { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Reason { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Comment { get; set; }

        /// <summary>Work types: ac, paint, pest_control, other.</summary>
        public List<string>? Categories { get; set; }
    }
}
