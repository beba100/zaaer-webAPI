using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Services.Auth
{
    /// <summary>
    /// Validates session version, account state, and required session id on each JWT request.
    /// </summary>
    public class JwtSessionValidationService
    {
        private readonly ISessionService _sessionService;
        private readonly ILogger<JwtSessionValidationService> _logger;

        public JwtSessionValidationService(
            ISessionService sessionService,
            ILogger<JwtSessionValidationService> logger)
        {
            _sessionService = sessionService;
            _logger = logger;
        }

        public async Task ValidatePrincipalAsync(ClaimsPrincipal principal)
        {
            var userIdClaim = principal.FindFirst("userId")?.Value;
            if (!int.TryParse(userIdClaim, out var userId) || userId <= 0)
            {
                throw new SecurityTokenValidationException("Missing userId claim");
            }

            var authState = await _sessionService.GetUserAuthStateAsync(userId);
            if (authState == null || !authState.IsActive)
            {
                throw new SecurityTokenValidationException("User inactive");
            }

            if (authState.IsLocked)
            {
                throw new SecurityTokenValidationException("User locked");
            }

            var svClaim = principal.FindFirst("sv")?.Value;
            if (string.IsNullOrWhiteSpace(svClaim))
            {
                _logger.LogWarning(
                    "[SECURITY] Rejecting JWT missing session version claim. UserId: {UserId}",
                    userId);
                throw new SecurityTokenValidationException("Missing session version");
            }

            if (!int.TryParse(svClaim, out var tokenVersion))
            {
                _logger.LogWarning(
                    "[SECURITY] Rejecting JWT with invalid session version claim. UserId: {UserId}, sv: {SessionVersionClaim}",
                    userId,
                    svClaim);
                throw new SecurityTokenValidationException("Invalid session version");
            }

            if (tokenVersion != authState.SessionVersion)
            {
                _logger.LogWarning(
                    "[SECURITY] Rejecting JWT with session version mismatch. UserId: {UserId}, TokenVersion: {TokenVersion}, CurrentVersion: {CurrentVersion}",
                    userId,
                    tokenVersion,
                    authState.SessionVersion);
                throw new SecurityTokenValidationException("Session version mismatch");
            }

            var sidClaim = principal.FindFirst("sid")?.Value;
            if (string.IsNullOrWhiteSpace(sidClaim))
            {
                _logger.LogWarning(
                    "[SECURITY] Rejecting legacy JWT missing session id claim. UserId: {UserId}",
                    userId);
                throw new SecurityTokenValidationException("Missing session id");
            }

            if (!long.TryParse(sidClaim, out var sessionId) || sessionId <= 0)
            {
                _logger.LogWarning(
                    "[SECURITY] Rejecting JWT with invalid session id claim. UserId: {UserId}, sid: {SessionIdClaim}",
                    userId,
                    sidClaim);
                throw new SecurityTokenValidationException("Invalid session id");
            }

            var active = await _sessionService.IsSessionActiveAsync(sessionId);
            if (!active)
            {
                _logger.LogWarning(
                    "[SECURITY] Rejecting JWT for revoked session. UserId: {UserId}, SessionId: {SessionId}",
                    userId,
                    sessionId);
                throw new SecurityTokenValidationException("Session revoked");
            }
        }

        public JwtBearerEvents CreateJwtBearerEvents()
        {
            return new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    try
                    {
                        if (context.Principal != null)
                        {
                            await ValidatePrincipalAsync(context.Principal);
                        }
                    }
                    catch (SecurityTokenValidationException ex)
                    {
                        _logger.LogInformation("JWT session validation failed: {Reason}", ex.Message);
                        context.Fail(ex.Message);
                    }
                },
                OnChallenge = context =>
                {
                    if (string.IsNullOrWhiteSpace(context.Error))
                    {
                        context.Error = "SESSION_REVOKED";
                    }

                    return Task.CompletedTask;
                }
            };
        }
    }
}
