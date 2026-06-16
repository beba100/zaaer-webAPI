using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using zaaerIntegration.Data;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Security;

namespace zaaerIntegration.Services.Implementations
{
    public class PermissionService : IPermissionService
    {
        private const string PermissionCachePrefix = "rbac:permissions:";
        private static readonly TimeSpan PermissionCacheDuration = TimeSpan.FromSeconds(90);

        private readonly MasterDbContext _masterDbContext;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PermissionService> _logger;

        public PermissionService(
            MasterDbContext masterDbContext,
            IMemoryCache cache,
            ILogger<PermissionService> logger)
        {
            _masterDbContext = masterDbContext;
            _cache = cache;
            _logger = logger;
        }

        public async Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(
            int userId,
            int tenantId,
            string authMode,
            CancellationToken cancellationToken = default)
        {
            _ = tenantId;
            _ = authMode;
            return await GetMasterPermissionsAsync(userId, cancellationToken);
        }

        public async Task<bool> HasPermissionAsync(
            int userId,
            int tenantId,
            string permissionCode,
            string authMode,
            CancellationToken cancellationToken = default)
        {
            var permissions = await GetEffectivePermissionsAsync(userId, tenantId, authMode, cancellationToken);
            return HasPermissionInSet(permissions, permissionCode);
        }

        public async Task<bool> HasAnyPermissionAsync(
            int userId,
            int tenantId,
            IEnumerable<string> permissionCodes,
            string authMode,
            CancellationToken cancellationToken = default)
        {
            if (permissionCodes == null)
            {
                return false;
            }

            var codes = permissionCodes
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (codes.Count == 0)
            {
                return false;
            }

            var permissions = await GetEffectivePermissionsAsync(userId, tenantId, authMode, cancellationToken);
            return codes.Any(code => HasPermissionInSet(permissions, code));
        }

        internal static bool HasPermissionInSet(IReadOnlyCollection<string> permissions, string permissionCode)
        {
            if (string.IsNullOrWhiteSpace(permissionCode))
            {
                return false;
            }

            var normalized = permissionCode.Trim();

            if (NavMenuLegacyPermissionMap.TryResolveNavMenuCode(normalized, out var navMenuCode)
                && !string.Equals(normalized, navMenuCode, StringComparison.OrdinalIgnoreCase))
            {
                return permissions.Contains(navMenuCode, StringComparer.OrdinalIgnoreCase);
            }

            if (permissions.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            if (TryGetReportParentCode(normalized, out var parentCode)
                && permissions.Contains(parentCode, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static bool TryGetReportParentCode(string permissionCode, out string parentCode)
        {
            parentCode = string.Empty;
            foreach (var prefix in new[] { "hotel.reports.", "resort.reports.", "hall.reports." })
            {
                if (permissionCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    parentCode = prefix.TrimEnd('.');
                    return true;
                }
            }

            return false;
        }

        private async Task<IReadOnlyCollection<string>> GetMasterPermissionsAsync(
            int rbacUserId,
            CancellationToken cancellationToken)
        {
            var cacheKey = $"{PermissionCachePrefix}{rbacUserId}";
            if (_cache.TryGetValue(cacheKey, out IReadOnlyCollection<string>? cached) && cached != null)
            {
                return cached;
            }

            try
            {
                var permissions = await (
                        from userRole in _masterDbContext.RbacUserRoles.AsNoTracking()
                        join rolePermission in _masterDbContext.RbacRolePermissions.AsNoTracking()
                            on userRole.RoleId equals rolePermission.RoleId
                        join permission in _masterDbContext.RbacPermissions.AsNoTracking()
                            on rolePermission.PermissionId equals permission.PermissionId
                        where userRole.IsActive
                              && userRole.UserId == rbacUserId
                              && rolePermission.Granted
                              && permission.IsActive
                        select permission.PermissionCode)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                _cache.Set(cacheKey, permissions, PermissionCacheDuration);
                return permissions;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SECURITY] RBAC permission lookup failed for UserId {UserId}", rbacUserId);
                return Array.Empty<string>();
            }
        }
    }
}
