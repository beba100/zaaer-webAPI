#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.RoomBoard
{
    public sealed class RoomBoardQuickStateRequestDto
    {
        /// <summary>
        /// One of: setCleaning, clearCleaning, setMaintenance, clearMaintenance.
        /// </summary>
        public string? Mode { get; set; }
    }
}
