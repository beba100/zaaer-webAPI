using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace zaaerIntegration.Configuration
{
    public sealed class JwtOptions
    {
        public const string SectionName = "Jwt";

        private const string InsecureDefaultSecret = "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!";
        private const string DevelopmentFallbackSecret = "DevelopmentOnly_JwtSecret_DoNotUseInStagingOrProduction_2026";
        private static int _devFallbackWarningLogged;

        public string SecretKey { get; set; } = string.Empty;
        public string Issuer { get; set; } = "ZaaerIntegration";
        public string Audience { get; set; } = "ZaaerIntegration";
        public int AccessTokenMinutes { get; set; } = 15;
        public int RefreshTokenDays { get; set; } = 30;

        public static JwtOptions ResolveAndValidate(IConfiguration config, IHostEnvironment env, ILogger logger)
        {
            var section = config.GetSection(SectionName);
            var secretKey = section["SecretKey"]?.Trim();
            var issuer = section["Issuer"];
            var audience = section["Audience"];
            var accessTokenMinutes = section.GetValue<int?>("AccessTokenMinutes") ?? 15;
            var refreshTokenDays = section.GetValue<int?>("RefreshTokenDays") ?? 30;

            if (string.IsNullOrWhiteSpace(issuer))
            {
                issuer = "ZaaerIntegration";
            }

            if (string.IsNullOrWhiteSpace(audience))
            {
                audience = "ZaaerIntegration";
            }

            var isProductionOrStaging = env.IsProduction() || env.IsStaging();
            if (isProductionOrStaging)
            {
                if (string.IsNullOrWhiteSpace(secretKey))
                {
                    throw new InvalidOperationException(
                        "JWT configuration error: Jwt:SecretKey is required in Staging/Production and must be at least 32 characters.");
                }

                if (secretKey.Length < 32)
                {
                    throw new InvalidOperationException(
                        "JWT configuration error: Jwt:SecretKey must be at least 32 characters in Staging/Production.");
                }

                if (string.Equals(secretKey, InsecureDefaultSecret, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "JWT configuration error: insecure default Jwt:SecretKey is not allowed in Staging/Production.");
                }
            }
            else if (env.IsDevelopment() && string.IsNullOrWhiteSpace(secretKey))
            {
                secretKey = DevelopmentFallbackSecret;
                if (Interlocked.Exchange(ref _devFallbackWarningLogged, 1) == 0)
                {
                    logger.LogWarning(
                        "Jwt:SecretKey is not configured in Development. Falling back to a development-only default secret.");
                }
            }

            if (string.IsNullOrWhiteSpace(secretKey))
            {
                throw new InvalidOperationException(
                    "JWT configuration error: Jwt:SecretKey is missing. Configure a non-empty secret.");
            }

            return new JwtOptions
            {
                SecretKey = secretKey,
                Issuer = issuer,
                Audience = audience,
                AccessTokenMinutes = accessTokenMinutes,
                RefreshTokenDays = refreshTokenDays
            };
        }
    }
}
