namespace zaaerIntegration.Services.Interfaces
{
    public interface IRbacSyncService
    {
        Task<int> SyncPermissionsToTenantAsync(int tenantId, CancellationToken cancellationToken = default);
    }
}
