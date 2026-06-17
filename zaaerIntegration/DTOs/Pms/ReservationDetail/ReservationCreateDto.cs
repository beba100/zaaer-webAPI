#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.Pms.ReservationDetail
{
    /// <summary>
    /// Create a reservation from the room board / PMS editor in one request (apartment + guest required).
    /// </summary>
    public sealed class ReservationCreateDto : ReservationPmsPatchDto
    {
        /// <summary>Board apartment id (Zaaer id or internal id).</summary>
        public int ApartmentId { get; init; }
    }
}
