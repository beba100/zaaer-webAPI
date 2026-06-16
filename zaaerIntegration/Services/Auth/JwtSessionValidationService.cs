using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Services.Auth
{
    /// <summary>
    /// Validates session version, account state, and optional session id on each JWT request.
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
            if (int.TryParse(svClaim, out var tokenVersion) && tokenVersion != authState.SessionVersion)
            {
                throw new SecurityTokenValidationException("Session version mismatch");
            }

            var sidClaim = principal.FindFirst("sid")?.Value;
            if (long.TryParse(sidClaim, out var sessionId) && sessionId > 0)
            {
                var active = await _sessionService.IsSessionActiveAsync(sessionId);
                if (!active)
                {
                    throw new SecurityTokenValidationException("Session revoked");
                }
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
