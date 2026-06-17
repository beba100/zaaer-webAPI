#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.Pms.ReservationDetail
{
    /// <summary>
    /// Start a new in-PMS reservation from a vacant room (uses board <c>apartmentId</c>: Zaaer id or internal id).
    /// </summary>
    public sealed class ReservationDraftCreateDto
    {
        public int ApartmentId { get; init; }
    }
}
