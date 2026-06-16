namespace zaaerIntegration.Services.Interfaces
{
    public interface IPermissionService
    {
        Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(
            int userId,
            int tenantId,
            string authMode,
            CancellationToken cancellationToken = default);

        Task<bool> HasPermissionAsync(
            int userId,
            int tenantId,
            string permissionCode,
            string authMode,
            CancellationToken cancellationToken = default);

        Task<bool> HasAnyPermissionAsync(
            int userId,
            int tenantId,
            IEnumerable<string> permissionCodes,
            string authMode,
            CancellationToken cancellationToken = default);
    }
}
