using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace zaaerIntegration.Security
{
    /// <summary>
    /// Shared API key and caller IP validation for background job endpoints.
    /// </summary>
    public static class JobsApiSecurityHelper
    {
        public static bool ValidateApiKey(IConfiguration config, string configKeyPath, string? providedKey)
        {
            var configuredKey = config[configKeyPath];
            if (string.IsNullOrWhiteSpace(configuredKey))
            {
                return false;
            }

            if (string.IsNullOrEmpty(providedKey))
            {
                return false;
            }

            var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
            var providedBytes = Encoding.UTF8.GetBytes(providedKey);

            return configuredBytes.Length == providedBytes.Length
                && CryptographicOperations.FixedTimeEquals(configuredBytes, providedBytes);
        }

        public static bool ValidateCallerIp(HttpContext context, IConfiguration config, string allowedIpsSectionPath)
        {
            var allowedIps = config.GetSection(allowedIpsSectionPath).Get<string[]>();
            if (allowedIps == null || allowedIps.Length == 0)
            {
                return true;
            }

            var remoteIp = context.Connection.RemoteIpAddress;
            if (remoteIp == null)
            {
                return false;
            }

            if (remoteIp.IsIPv4MappedToIPv6)
            {
                remoteIp = remoteIp.MapToIPv4();
            }

            foreach (var allowed in allowedIps)
            {
                if (string.IsNullOrWhiteSpace(allowed))
                {
                    continue;
                }

                if (!IPAddress.TryParse(allowed.Trim(), out var allowedIp))
                {
                    continue;
                }

                if (allowedIp.IsIPv4MappedToIPv6)
                {
                    allowedIp = allowedIp.MapToIPv4();
                }

                if (allowedIp.Equals(remoteIp))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
