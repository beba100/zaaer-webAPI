namespace zaaerIntegration.Services.Interfaces
{
    public interface ICurrentUserContext
    {
        bool IsAuthenticated { get; }
        int? UserId { get; }
        int? TenantId { get; }
        string? TenantCode { get; }
        string? Username { get; }
        string AuthMode { get; }
        IReadOnlyCollection<string> Roles { get; }
        IReadOnlyCollection<string> Permissions { get; }
        IReadOnlyCollection<int> AllowedHotelIds { get; }
        IReadOnlyCollection<int> AllowedGroupIds { get; }
        bool HasPermission(string permissionCode);
        bool CanAccessHotel(int tenantId);
    }
}
