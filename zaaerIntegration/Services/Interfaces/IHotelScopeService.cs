using FinanceLedgerAPI.Models;

namespace zaaerIntegration.Services.Interfaces
{
    /// <summary>
    /// Resolves Master DB tenants visible to the authenticated user for cross-hotel reports and lookups.
    /// </summary>
    public interface IHotelScopeService
    {
        /// <summary>
        /// Returns tenants assigned to the current user. When <paramref name="hotelCodesCsv"/> is provided,
        /// results are filtered to those codes and forbidden codes throw <see cref="UnauthorizedAccessException"/>.
        /// </summary>
        Task<IReadOnlyList<Tenant>> ResolveTenantsAsync(
            string? hotelCodesCsv = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds a tenant by code only when the current user is allowed to access it.
        /// </summary>
        Task<Tenant?> FindAccessibleTenantByCodeAsync(
            string code,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns true when the current user may access the given Master tenant id (Master DB source of truth).
        /// </summary>
        Task<bool> CanAccessTenantIdAsync(int tenantId, CancellationToken cancellationToken = default);
    }
}
