namespace zaaerIntegration.Security
{
    /// <summary>
    /// Maps legacy functional RBAC codes to nav.menu.* menu permissions.
    /// Menu visibility and system-page access follow nav.menu.* — not legacy alone.
    /// </summary>
    public static class NavMenuLegacyPermissionMap
    {
        public static readonly IReadOnlyList<(string LegacyCode, string NavMenuCode)> SystemPairs =
        [
            ("rbac.users.manage", "nav.menu.system.users"),
            ("rbac.roles.manage", "nav.menu.system.roles"),
            ("rbac.permissions.view", "nav.menu.system.permissions"),
            ("admin.numbering.manage", "nav.menu.system.numbering")
        ];

        private static readonly Dictionary<string, string> LegacyToNav = SystemPairs
            .ToDictionary(x => x.LegacyCode, x => x.NavMenuCode, StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> NavToLegacy = SystemPairs
            .ToDictionary(x => x.NavMenuCode, x => x.LegacyCode, StringComparer.OrdinalIgnoreCase);

        public static bool TryResolveNavMenuCode(string permissionCode, out string navMenuCode)
        {
            navMenuCode = string.Empty;
            if (string.IsNullOrWhiteSpace(permissionCode))
            {
                return false;
            }

            if (permissionCode.StartsWith("nav.menu.", StringComparison.OrdinalIgnoreCase))
            {
                navMenuCode = permissionCode.Trim();
                return true;
            }

            return LegacyToNav.TryGetValue(permissionCode.Trim(), out navMenuCode!);
        }

        public static bool TryResolveLegacyCode(string navMenuCode, out string legacyCode)
        {
            legacyCode = string.Empty;
            return !string.IsNullOrWhiteSpace(navMenuCode)
                && NavToLegacy.TryGetValue(navMenuCode.Trim(), out legacyCode!);
        }
    }
}
