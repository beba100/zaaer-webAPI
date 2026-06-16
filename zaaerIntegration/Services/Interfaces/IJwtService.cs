using System.Security.Claims;
using zaaerIntegration.Services.Auth;

namespace zaaerIntegration.Services.Interfaces
{
    /// <summary>
    /// Interface for JWT Service
    /// </summary>
    public interface IJwtService
    {
        /// <summary>
        /// إنشاء JWT Token
        /// </summary>
        string GenerateToken(int userId, string username, int tenantId, IEnumerable<string> roles);

        /// <summary>
        /// إنشاء JWT Token غني بسياق RBAC ونطاق الفنادق.
        /// </summary>
        string GenerateToken(JwtTokenDescriptor descriptor);

        /// <summary>
        /// Access token lifetime in minutes.
        /// </summary>
        int AccessTokenMinutes { get; }

        /// <summary>
        /// التحقق من صحة Token
        /// </summary>
        ClaimsPrincipal? ValidateToken(string token);
    }
}

