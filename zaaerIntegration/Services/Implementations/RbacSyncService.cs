using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    /// <summary>
    /// Synchronizes central RBAC definitions into the current tenant database.
    /// </summary>
    public class RbacSyncService : IRbacSyncService
    {
        private readonly MasterDbContext _masterDbContext;
        private readonly ApplicationDbContext _applicationDbContext;

        public RbacSyncService(MasterDbContext masterDbContext, ApplicationDbContext applicationDbContext)
        {
            _masterDbContext = masterDbContext;
            _applicationDbContext = applicationDbContext;
        }

        public async Task<int> SyncPermissionsToTenantAsync(int tenantId, CancellationToken cancellationToken = default)
        {
            var masterPermissions = await _masterDbContext.RbacPermissions
                .AsNoTracking()
                .Where(x => x.IsActive)
                .ToListAsync(cancellationToken);

            var tenantPermissions = await _applicationDbContext.Permissions
                .ToDictionaryAsync(x => x.PermissionCode, StringComparer.OrdinalIgnoreCase, cancellationToken);

            var changed = 0;
            foreach (var source in masterPermissions)
            {
                if (tenantPermissions.TryGetValue(source.PermissionCode, out var target))
                {
                    if (target.PermissionName != source.PermissionName ||
                        target.ModuleName != source.ModuleName ||
                        target.ActionName != source.ActionName ||
                        target.Description != source.Description ||
                        target.IsActive != source.IsActive)
                    {
                        target.PermissionName = source.PermissionName;
                        target.ModuleName = source.ModuleName;
                        target.ActionName = source.ActionName;
                        target.Description = source.Description;
                        target.IsActive = source.IsActive;
                        changed++;
                    }
                }
                else
                {
                    _applicationDbContext.Permissions.Add(new FinanceLedgerAPI.Models.Permission
                    {
                        PermissionName = source.PermissionName,
                        PermissionCode = source.PermissionCode,
                        ModuleName = source.ModuleName,
                        ActionName = source.ActionName,
                        Description = source.Description,
                        IsActive = source.IsActive,
                        CreatedAt = KsaTime.Now
                    });
                    changed++;
                }
            }

            if (changed > 0)
            {
                await _applicationDbContext.SaveChangesAsync(cancellationToken);
            }

            return changed;
        }
    }
}
