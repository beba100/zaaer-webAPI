using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using FinanceLedgerAPI.Models;
using zaaerIntegration.Security;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services
{
    /// <summary>
    /// Keeps legacy functional grants aligned with nav.menu.system.* when roles are saved.
    /// </summary>
    public static class NavMenuLegacyPermissionSync
    {
        public static async Task ApplyAsync(MasterDbContext db, int roleId, CancellationToken cancellationToken = default)
        {
            var codes = NavMenuLegacyPermissionMap.SystemPairs
                .SelectMany(p => new[] { p.LegacyCode, p.NavMenuCode })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var permissions = await db.RbacPermissions
                .AsNoTracking()
                .Where(p => codes.Contains(p.PermissionCode) && p.IsActive)
                .ToListAsync(cancellationToken);

            var byCode = permissions.ToDictionary(
                p => p.PermissionCode,
                p => p.PermissionId,
                StringComparer.OrdinalIgnoreCase);

            var grantedIds = (await db.RbacRolePermissions
                .AsNoTracking()
                .Where(rp => rp.RoleId == roleId && rp.Granted)
                .Select(rp => rp.PermissionId)
                .ToListAsync(cancellationToken))
                .ToHashSet();

            var existingLegacy = await db.RbacRolePermissions
                .Where(rp => rp.RoleId == roleId)
                .ToListAsync(cancellationToken);

            var byPermissionId = existingLegacy.ToDictionary(x => x.PermissionId);
            var now = KsaTime.Now;

            foreach (var (legacyCode, navCode) in NavMenuLegacyPermissionMap.SystemPairs)
            {
                if (!byCode.TryGetValue(navCode, out var navPermissionId)
                    || !byCode.TryGetValue(legacyCode, out var legacyPermissionId))
                {
                    continue;
                }

                var navGranted = grantedIds.Contains(navPermissionId);
                if (!byPermissionId.TryGetValue(legacyPermissionId, out var legacyRow))
                {
                    if (navGranted)
                    {
                        db.RbacRolePermissions.Add(new MasterRbacRolePermission
                        {
                            RoleId = roleId,
                            PermissionId = legacyPermissionId,
                            Granted = true,
                            CreatedAt = now
                        });
                    }

                    continue;
                }

                legacyRow.Granted = navGranted;
            }
        }
    }
}
