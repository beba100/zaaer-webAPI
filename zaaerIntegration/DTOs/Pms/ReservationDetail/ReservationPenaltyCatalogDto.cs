#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.Pms.ReservationDetail
{
    public sealed class ReservationPenaltyCatalogDto
    {
        public int PenaltyId { get; init; }
        public int HotelId { get; init; }
        public int ReservationId { get; init; }
        public string PenaltyType { get; init; } = string.Empty;
        public string PenaltyName { get; init; } = string.Empty;
        public string? PenaltyNameAr { get; init; }
        public string? Description { get; init; }
        public decimal BaseAmount { get; init; }
        public bool IsActive { get; init; }
    }

    public sealed class CreateReservationPenaltyCatalogDto
    {
        public int? HotelId { get; init; }
        public int? ReservationId { get; init; }
        public string? PenaltyType { get; init; }
        public string PenaltyName { get; init; } = string.Empty;
        public string? PenaltyNameAr { get; init; }
        public string? Description { get; init; }
        public decimal BaseAmount { get; init; }
    }
}
