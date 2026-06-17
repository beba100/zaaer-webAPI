#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.Response
{
    public sealed class CustomerIdentificationResponseDto
    {
        public int IdentificationId { get; init; }
        public int IdTypeId { get; init; }
        public string? IdTypeName { get; init; }
        public string? IdTypeNameAr { get; init; }
        public string IdNumber { get; init; } = string.Empty;
        public string? VersionNumber { get; init; }
        public bool IsPrimary { get; init; }
    }
}
