using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using zaaerIntegration.Configuration;
using zaaerIntegration.Services.Auth;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    /// <summary>
    /// Service for JWT Token operations
    /// </summary>
    public class JwtService : IJwtService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<JwtService> _logger;
        private readonly string _secretKey;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly int _accessTokenMinutes;

        /// <summary>
        /// Constructor for JwtService
        /// </summary>
        /// <param name="configuration">Configuration</param>
        /// <param name="jwtOptions">Validated JWT options from startup</param>
        /// <param name="logger">Logger</param>
        public JwtService(IConfiguration configuration, IOptions<JwtOptions> jwtOptions, ILogger<JwtService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var options = jwtOptions?.Value ?? throw new ArgumentNullException(nameof(jwtOptions));
            _secretKey = options.SecretKey;
            _issuer = options.Issuer;
            _audience = options.Audience;
            _accessTokenMinutes = options.AccessTokenMinutes > 0
                ? options.AccessTokenMinutes
                : configuration.GetValue("Jwt:ExpirationMinutes", 1440);
        }

        /// <summary>
        /// Access token lifetime in minutes.
        /// </summary>
        public int AccessTokenMinutes => _accessTokenMinutes;

        /// <summary>
        /// إنشاء JWT Token
        /// </summary>
        public string GenerateToken(int userId, string username, int tenantId, IEnumerable<string> roles)
        {
            return GenerateToken(new JwtTokenDescriptor
            {
                UserId = userId,
                Username = username,
                TenantId = tenantId,
                Roles = roles ?? Array.Empty<string>(),
                AllowedHotelIds = new[] { tenantId }
            });
        }

        /// <summary>
        /// إنشاء JWT Token غني بسياق RBAC.
        /// </summary>
        public string GenerateToken(JwtTokenDescriptor descriptor)
        {
            // ✅ التحقق من TenantId (مطلوب)
            if (descriptor.TenantId <= 0)
            {
                _logger.LogError("❌ Cannot generate token: Invalid TenantId. UserId: {UserId}, TenantId: {TenantId}", 
                    descriptor.UserId, descriptor.TenantId);
                throw new ArgumentException("TenantId must be greater than 0", nameof(descriptor.TenantId));
            }

            var roles = (descriptor.Roles ?? Array.Empty<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var permissions = (descriptor.Permissions ?? Array.Empty<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var allowedHotelIds = (descriptor.AllowedHotelIds ?? Array.Empty<int>())
                .Where(id => id > 0)
                .DefaultIfEmpty(descriptor.TenantId)
                .Distinct()
                .ToList();
            var allowedGroupIds = (descriptor.AllowedGroupIds ?? Array.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, descriptor.UserId.ToString()),
                new Claim(ClaimTypes.Name, descriptor.Username),
                new Claim("userId", descriptor.UserId.ToString()),
                new Claim("tenantId", descriptor.TenantId.ToString()),
                new Claim("username", descriptor.Username),
                new Claim("authMode", descriptor.AuthMode),
                new Claim("allowedHotelIds", string.Join(",", allowedHotelIds)),
                new Claim("allowedGroupIds", string.Join(",", allowedGroupIds))
            };

            if (!string.IsNullOrWhiteSpace(descriptor.TenantCode))
            {
                claims.Add(new Claim("tenantCode", descriptor.TenantCode));
            }

            // إضافة الأدوار
            if (roles.Any())
            {
                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
                // إضافة roles كـ comma-separated string للسهولة
                claims.Add(new Claim("roles", string.Join(",", roles)));
            }

            // Keep a single CSV claim only — duplicating each code as its own "permission"
            // claim balloons the JWT and can exceed IIS request-header limits (HTTP 400).
            if (permissions.Any())
            {
                claims.Add(new Claim("permissions", string.Join(",", permissions)));
            }

            if (descriptor.SessionId.HasValue && descriptor.SessionId.Value > 0)
            {
                claims.Add(new Claim("sid", descriptor.SessionId.Value.ToString()));
            }

            claims.Add(new Claim("sv", descriptor.SessionVersion.ToString()));
            claims.Add(new Claim("jti", Guid.NewGuid().ToString("N")));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: KsaTime.UtcNow.AddMinutes(_accessTokenMinutes),
                signingCredentials: credentials
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            _logger.LogDebug(
                "JWT Token generated for user: {Username}, TenantId: {TenantId}, AuthMode: {AuthMode}",
                descriptor.Username,
                descriptor.TenantId,
                descriptor.AuthMode);

            return tokenString;
        }

        /// <summary>
        /// التحقق من صحة Token
        /// </summary>
        public ClaimsPrincipal? ValidateToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _issuer,
                    ValidateAudience = true,
                    ValidAudience = _audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
                return principal;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed");
                return null;
            }
        }
    }
}

