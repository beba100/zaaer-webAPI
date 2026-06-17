namespace zaaerIntegration.DTOs.Admin
{
    public sealed record SeedTenantRequestDto(
        int? TenantId,
        string? TenantCode);

    public sealed record EnsureDocumentCountersRequestDto(
        int TenantId,
        int HotelZaaerId,
        int LocalHotelId);

    public sealed record CreateOrUpdateTenantRequestDto(
        int? Id,
        string Code,
        string Name,
        string? NameEn,
        string DatabaseName,
        int? ZaaerId);
}

