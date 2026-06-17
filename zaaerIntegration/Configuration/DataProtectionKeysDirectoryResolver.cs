namespace zaaerIntegration.Configuration
{
    /// <summary>
    /// Resolves ASP.NET Data Protection key ring folder for IIS/MonsterASP layouts.
    /// Older deployments stored keys under <c>wwwroot/App_Data/DataProtection-Keys</c>;
    /// newer defaults use <c>App_Data/DataProtection-Keys</c> at the content root.
    /// </summary>
    internal static class DataProtectionKeysDirectoryResolver
    {
        private const string LegacyRelativePath = "wwwroot/App_Data/DataProtection-Keys";
        private const string DefaultRelativePath = "App_Data/DataProtection-Keys";

        internal static string Resolve(string contentRootPath, string? configuredRelativePath)
        {
            var configured = NormalizeRelative(contentRootPath, configuredRelativePath);
            var legacy = NormalizeRelative(contentRootPath, LegacyRelativePath);
            var fallback = NormalizeRelative(contentRootPath, DefaultRelativePath);

            var candidates = new[] { configured, legacy, fallback }
                .Where(static p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var dir in candidates)
            {
                if (HasKeyFiles(dir))
                {
                    return dir;
                }
            }

            return configured ?? legacy;
        }

        internal static int CountKeyFiles(string directory) =>
            Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, "key-*.xml").Count()
                : 0;

        private static string? NormalizeRelative(string contentRootPath, string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            var trimmed = relativePath.Trim();
            return Path.IsPathRooted(trimmed)
                ? Path.GetFullPath(trimmed)
                : Path.GetFullPath(Path.Combine(contentRootPath, trimmed));
        }

        private static bool HasKeyFiles(string directory) =>
            Directory.Exists(directory) && Directory.EnumerateFiles(directory, "key-*.xml").Any();
    }
}
