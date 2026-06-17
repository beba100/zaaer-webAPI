using FinanceLedgerAPI.Models;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Services.Implementations
{
    public class AuthModeResolver : IAuthModeResolver
    {
        public Task<string> ResolveForTenantAsync(int tenantId, CancellationToken cancellationToken = default)
        {
            _ = tenantId;
            _ = cancellationToken;
            return Task.FromResult(AuthModes.CentralManaged);
        }
    }
}
