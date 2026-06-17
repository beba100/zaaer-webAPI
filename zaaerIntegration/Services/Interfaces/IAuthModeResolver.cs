namespace zaaerIntegration.Services.Interfaces
{
    public interface IAuthModeResolver
    {
        Task<string> ResolveForTenantAsync(int tenantId, CancellationToken cancellationToken = default);
    }
}
