#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.Pms.ReservationDetail
{
    public sealed class ReservationPackageDto
    {
        public int PackageId { get; init; }
        public int? HotelId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? NameAr { get; init; }
        public string? Description { get; init; }
        public decimal UnitPrice { get; init; }
        public bool IsActive { get; init; }
        public int SortOrder { get; init; }
    }

    public sealed class CreateReservationPackageDto
    {
        public int? HotelId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? NameAr { get; init; }
        public string? Description { get; init; }
        public decimal UnitPrice { get; init; }
        public bool IsActive { get; init; } = true;
        public int SortOrder { get; init; } = 100;
    }
}
