using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using FinanceLedgerAPI.Models;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Auth;
using zaaerIntegration.Services.Auth;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Controllers
{
    /// <summary>
    /// Controller للتعامل مع Authentication
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IRbacUserService _rbacUserService;
        private readonly IJwtService _jwtService;
        private readonly IEmailService _emailService;
        private readonly IWhatsAppService _whatsAppService;
        private readonly ILogger<AuthController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly MasterDbContext _masterDbContext;
        private readonly IAuthModeResolver _authModeResolver;
        private readonly IHotelAccessService _hotelAccessService;
        private readonly IPermissionService _permissionService;
        private readonly IResortTicketGateLandingService _gateLandingService;
        private readonly ISessionService _sessionService;

        /// <summary>
        /// Creates the authentication controller.
        /// </summary>
        public AuthController(
            IRbacUserService rbacUserService,
            IJwtService jwtService,
            IEmailService emailService,
            IWhatsAppService whatsAppService,
            ILogger<AuthController> logger,
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            MasterDbContext masterDbContext,
            IAuthModeResolver authModeResolver,
            IHotelAccessService hotelAccessService,
            IPermissionService permissionService,
            IResortTicketGateLandingService gateLandingService,
            ISessionService sessionService)
        {
            _rbacUserService = rbacUserService ?? throw new ArgumentNullException(nameof(rbacUserService));
            _jwtService = jwtService ?? throw new ArgumentNullException(nameof(jwtService));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _whatsAppService = whatsAppService ?? throw new ArgumentNullException(nameof(whatsAppService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _masterDbContext = masterDbContext ?? throw new ArgumentNullException(nameof(masterDbContext));
            _authModeResolver = authModeResolver ?? throw new ArgumentNullException(nameof(authModeResolver));
            _hotelAccessService = hotelAccessService ?? throw new ArgumentNullException(nameof(hotelAccessService));
            _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
            _gateLandingService = gateLandingService ?? throw new ArgumentNullException(nameof(gateLandingService));
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        }

        /// <summary>
        /// تسجيل الدخول
        /// </summary>
        /// <param name="request">بيانات تسجيل الدخول</param>
        /// <returns>JWT Token وبيانات المستخدم</returns>
        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { error = "Invalid request", details = ModelState });
            }

            _logger.LogInformation("🔐 Login Request: Username='{Username}', HotelCode='{HotelCode}', TenantId={TenantId}",
                request.Username,
                request.HotelCode,
                request.TenantId);

            try
            {
                // ✅ 1. التحقق من بيانات تسجيل الدخول (يجب أن يكون أول شيء)
                // ✅ استخدام EmployeeNumber فقط للدخول
                var user = await _rbacUserService.ValidateLoginAsync(request.Username, request.Password);

                if (user == null)
                {
                    _logger.LogWarning("❌ Login failed: Invalid credentials. Login: {Login}", request.Username);
                    await _sessionService.LogSecurityEventAsync(
                        SecurityAuditEventTypes.LoginFailed,
                        null,
                        null,
                        null,
                        GetClientIpAddress(),
                        request.Username);
                    return Unauthorized(new { error = "بيانات الدخول غير صحيحة", code = "INVALID_CREDENTIALS" });
                }

                if (user.IsLocked)
                {
                    _logger.LogWarning("❌ Login failed: User locked. UserId: {UserId}", user.UserId);
                    return Unauthorized(new { error = "الحساب مقفول. تواصل مع المسؤول.", code = "USER_LOCKED" });
                }

                var allowedHotelIds = (await _hotelAccessService.GetAllowedTenantIdsAsync(user.UserId)).ToList();
                if (allowedHotelIds.Count == 0)
                {
                    _logger.LogError("❌ Login failed: User has no assigned hotels. UserId: {UserId}", user.UserId);
                    return Unauthorized(new { error = "لم يُعيَّن أي فندق لهذا المستخدم" });
                }

                var fallbackTenantId = allowedHotelIds[0];
                var requestedTenant = await ResolveRequestedTenantAsync(request, fallbackTenantId);
                var tenantId = requestedTenant?.Id ?? fallbackTenantId;

                if (!allowedHotelIds.Contains(tenantId))
                {
                    _logger.LogWarning(
                        "❌ Login failed: UserId {UserId} does not have access to TenantId {TenantId}",
                        user.UserId,
                        tenantId);
                    return Unauthorized(new { error = "ليس لديك صلاحية الدخول لهذا الفندق" });
                }

                var tenantCode = requestedTenant?.Code ?? "";
                var tenantName = requestedTenant?.Name ?? "";
                if (requestedTenant == null)
                {
                    var fallbackTenant = await _masterDbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId);
                    tenantCode = fallbackTenant?.Code ?? tenantCode;
                    tenantName = fallbackTenant?.Name ?? tenantName;
                }

                var authMode = await _authModeResolver.ResolveForTenantAsync(tenantId);
                var rolesList = (await _rbacUserService.GetUserRoleCodesAsync(user.UserId)).ToList();
                var permissions = (await _permissionService.GetEffectivePermissionsAsync(user.UserId, tenantId, authMode)).ToList();
                var fullName = $"{user.FirstName} {user.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = user.Username;
                }

                var fullNameEn = LocalizedDisplayNameHelper.UserFullNameEn(user);
                var tenantForNames = requestedTenant
                    ?? await _masterDbContext.Tenants.AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Id == tenantId);
                if (tenantForNames != null)
                {
                    tenantName = tenantForNames.Name ?? tenantName;
                }

                _logger.LogInformation("📋 [Login] UserId {UserId} (EmployeeNumber: {EmployeeNumber}, Username: {Username}) - Roles: {Roles}",
                    user.UserId, user.EmployeeNumber ?? "N/A", user.Username, string.Join(", ", rolesList));

                var (refreshPlain, sessionId, refreshExpiresAt) = await _sessionService.CreateSessionAsync(
                    user.UserId,
                    request.DeviceId,
                    request.DeviceName,
                    GetClientIpAddress(),
                    GetUserAgent(),
                    cancellationToken);

                var token = _jwtService.GenerateToken(new JwtTokenDescriptor
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    TenantId = tenantId,
                    TenantCode = tenantCode,
                    AuthMode = authMode,
                    Roles = rolesList,
                    Permissions = permissions,
                    AllowedHotelIds = allowedHotelIds,
                    AllowedGroupIds = new List<int>(),
                    SessionVersion = user.SessionVersion,
                    SessionId = sessionId
                });
                
                _logger.LogInformation("✅ [Login] JWT Token generated for UserId {UserId} with roles: {Roles}",
                    user.UserId, string.Join(", ", rolesList));

                var availableHotels = await _masterDbContext.Tenants
                    .AsNoTracking()
                    .Where(t => allowedHotelIds.Contains(t.Id))
                    .OrderBy(t => t.Name)
                    .Select(t => new AvailableHotelDto
                    {
                        TenantId = t.Id,
                        Code = t.Code ?? "",
                        Name = t.Name ?? "",
                        NameEn = t.NameEn
                    })
                    .ToListAsync();

                var response = new LoginResponseDto
                {
                    Token = token,
                    UserId = user.UserId,
                    Username = user.Username,
                    FullName = fullName,
                    FullNameEn = fullNameEn,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    EmployeeNumber = user.EmployeeNumber,
                    TenantId = tenantId,
                    TenantCode = tenantCode,
                    TenantName = tenantName,
                    TenantNameEn = tenantForNames == null
                        ? null
                        : LocalizedDisplayNameHelper.TenantNameEn(tenantForNames),
                    Roles = rolesList,
                    Permissions = permissions,
                    AllowedHotelIds = allowedHotelIds,
                    AllowedGroupIds = new List<int>(),
                    AuthMode = authMode,
                    ExpiresAt = KsaTime.Now.AddMinutes(_jwtService.AccessTokenMinutes),
                    RefreshToken = refreshPlain,
                    RefreshExpiresAt = refreshExpiresAt,
                    AvailableHotels = availableHotels
                };

                await ApplyGateLandingAsync(response, cancellationToken);

                await _sessionService.LogSecurityEventAsync(
                    SecurityAuditEventTypes.Login,
                    user.UserId,
                    user.UserId,
                    sessionId,
                    GetClientIpAddress(),
                    request.DeviceName);

                _logger.LogInformation("✅ Login successful: EmployeeNumber={EmployeeNumber}, Username={Username}, TenantId={TenantId}",
                    user.EmployeeNumber ?? "N/A", user.Username, tenantId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for employee number: {EmployeeNumber}", request.Username);
                return StatusCode(500, new { error = "حدث خطأ أثناء تسجيل الدخول", message = ex.Message });
            }
        }

        /// <summary>
        /// تبديل الفندق النشط بعد تسجيل الدخول (JWT + X-Hotel-Code).
        /// </summary>
        [HttpPost("switch-hotel")]
        [Authorize]
        [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> SwitchHotel([FromBody] SwitchHotelRequestDto request)
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            if (!int.TryParse(userIdClaim, out var userId) || userId <= 0)
            {
                return Unauthorized(new { error = "Authentication required" });
            }

            var allowedHotelIds = (await _hotelAccessService.GetAllowedTenantIdsAsync(userId)).ToList();
            if (allowedHotelIds.Count == 0)
            {
                return Unauthorized(new { error = "لم يُعيَّن أي فندق لهذا المستخدم" });
            }

            var fallbackTenantId = allowedHotelIds[0];
            var loginLike = new LoginRequestDto
            {
                HotelCode = request.HotelCode,
                TenantId = request.TenantId
            };

            var tenant = await ResolveRequestedTenantAsync(loginLike, fallbackTenantId);
            if (tenant == null || !allowedHotelIds.Contains(tenant.Id))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "ليس لديك صلاحية الدخول لهذا الفندق" });
            }

            var user = await _rbacUserService.GetByIdAsync(userId);
            if (user == null || !user.IsActive)
            {
                return Unauthorized(new { error = "المستخدم غير نشط" });
            }

            var authMode = await _authModeResolver.ResolveForTenantAsync(tenant.Id);
            var rolesList = (await _rbacUserService.GetUserRoleCodesAsync(userId)).ToList();
            var permissions = (await _permissionService.GetEffectivePermissionsAsync(userId, tenant.Id, authMode)).ToList();
            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(fullName))
            {
                fullName = user.Username;
            }

            var fullNameEn = LocalizedDisplayNameHelper.UserFullNameEn(user);
            var authState = await _sessionService.GetUserAuthStateAsync(userId);
            var sessionId = await _sessionService.EnsureActiveSessionAsync(
                userId,
                TryGetCurrentSessionId(),
                Request.Headers["X-Device-Id"].FirstOrDefault(),
                null,
                GetClientIpAddress(),
                GetUserAgent());

            var token = _jwtService.GenerateToken(new JwtTokenDescriptor
            {
                UserId = userId,
                Username = user.Username,
                TenantId = tenant.Id,
                TenantCode = tenant.Code ?? "",
                AuthMode = authMode,
                Roles = rolesList,
                Permissions = permissions,
                AllowedHotelIds = allowedHotelIds,
                AllowedGroupIds = new List<int>(),
                SessionVersion = authState?.SessionVersion ?? user.SessionVersion,
                SessionId = sessionId
            });

            var availableHotels = await _masterDbContext.Tenants
                .AsNoTracking()
                .Where(t => allowedHotelIds.Contains(t.Id))
                .OrderBy(t => t.Name)
                .Select(t => new AvailableHotelDto
                {
                    TenantId = t.Id,
                    Code = t.Code ?? "",
                    Name = t.Name ?? "",
                    NameEn = t.NameEn
                })
                .ToListAsync();

            var switchResponse = new LoginResponseDto
            {
                Token = token,
                UserId = userId,
                Username = user.Username,
                FullName = fullName,
                FullNameEn = fullNameEn,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                EmployeeNumber = user.EmployeeNumber,
                TenantId = tenant.Id,
                TenantCode = tenant.Code ?? "",
                TenantName = tenant.Name ?? "",
                TenantNameEn = LocalizedDisplayNameHelper.TenantNameEn(tenant),
                Roles = rolesList,
                Permissions = permissions,
                AllowedHotelIds = allowedHotelIds,
                AllowedGroupIds = new List<int>(),
                AuthMode = authMode,
                ExpiresAt = KsaTime.Now.AddMinutes(_jwtService.AccessTokenMinutes),
                AvailableHotels = availableHotels
            };

            await ApplyGateLandingAsync(switchResponse, HttpContext.RequestAborted);
            return Ok(switchResponse);
        }

        /// <summary>
        /// Effective permission codes for the signed-in user (fresh from DB, not cached JWT only).
        /// </summary>
        [HttpGet("my-permissions")]
        [Authorize]
        [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyPermissions(CancellationToken cancellationToken)
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            var tenantIdClaim = User.FindFirst("tenantId")?.Value;
            if (!int.TryParse(userIdClaim, out var userId) || userId <= 0 ||
                !int.TryParse(tenantIdClaim, out var tenantId) || tenantId <= 0)
            {
                return Unauthorized(new { error = "Authentication required" });
            }

            var authMode = User.FindFirst("authMode")?.Value ?? AuthModes.CentralManaged;
            var permissions = await _permissionService.GetEffectivePermissionsAsync(
                userId,
                tenantId,
                authMode,
                cancellationToken);

            return Ok(permissions);
        }

        /// <summary>
        /// Reloads effective permissions from Master RBAC and re-issues JWT (no password re-login).
        /// </summary>
        [HttpPost("refresh-session")]
        [Authorize]
        [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> RefreshSession(CancellationToken cancellationToken)
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            var tenantIdClaim = User.FindFirst("tenantId")?.Value;
            if (!int.TryParse(userIdClaim, out var userId) || userId <= 0 ||
                !int.TryParse(tenantIdClaim, out var tenantId) || tenantId <= 0)
            {
                return Unauthorized(new { error = "Authentication required" });
            }

            var user = await _rbacUserService.GetByIdAsync(userId);
            if (user == null || !user.IsActive)
            {
                return Unauthorized(new { error = "المستخدم غير نشط" });
            }

            var allowedHotelIds = (await _hotelAccessService.GetAllowedTenantIdsAsync(userId)).ToList();
            if (allowedHotelIds.Count == 0 || !allowedHotelIds.Contains(tenantId))
            {
                return Unauthorized(new { error = "لم يُعيَّن أي فندق لهذا المستخدم" });
            }

            var tenant = await _masterDbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
            var authMode = await _authModeResolver.ResolveForTenantAsync(tenantId);
            var rolesList = (await _rbacUserService.GetUserRoleCodesAsync(userId)).ToList();
            var permissions = (await _permissionService.GetEffectivePermissionsAsync(userId, tenantId, authMode, cancellationToken)).ToList();
            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(fullName))
            {
                fullName = user.Username;
            }

            var fullNameEn = LocalizedDisplayNameHelper.UserFullNameEn(user);
            var authState = await _sessionService.GetUserAuthStateAsync(userId, cancellationToken);
            var sessionId = await _sessionService.EnsureActiveSessionAsync(
                userId,
                TryGetCurrentSessionId(),
                Request.Headers["X-Device-Id"].FirstOrDefault(),
                null,
                GetClientIpAddress(),
                GetUserAgent(),
                cancellationToken);

            var token = _jwtService.GenerateToken(new JwtTokenDescriptor
            {
                UserId = userId,
                Username = user.Username,
                TenantId = tenantId,
                TenantCode = tenant?.Code ?? User.FindFirst("tenantCode")?.Value ?? "",
                AuthMode = authMode,
                Roles = rolesList,
                Permissions = permissions,
                AllowedHotelIds = allowedHotelIds,
                AllowedGroupIds = new List<int>(),
                SessionVersion = authState?.SessionVersion ?? user.SessionVersion,
                SessionId = sessionId
            });

            var refreshResponse = new LoginResponseDto
            {
                Token = token,
                UserId = userId,
                Username = user.Username,
                FullName = fullName,
                FullNameEn = fullNameEn,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                EmployeeNumber = user.EmployeeNumber,
                TenantId = tenantId,
                TenantCode = tenant?.Code ?? "",
                TenantName = tenant?.Name ?? "",
                TenantNameEn = tenant == null ? null : LocalizedDisplayNameHelper.TenantNameEn(tenant),
                Roles = rolesList,
                Permissions = permissions,
                AllowedHotelIds = allowedHotelIds,
                AllowedGroupIds = new List<int>(),
                AuthMode = authMode,
                ExpiresAt = KsaTime.Now.AddMinutes(_jwtService.AccessTokenMinutes)
            };

            await ApplyGateLandingAsync(refreshResponse, cancellationToken);
            return Ok(refreshResponse);
        }

        /// <summary>
        /// Exchange a refresh token for a new access token (and rotated refresh token).
        /// </summary>
        [HttpPost("refresh")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return Unauthorized(new { error = "Refresh token required", code = "SESSION_EXPIRED" });
            }

            var rotated = await _sessionService.RotateRefreshTokenAsync(
                request.RefreshToken,
                request.DeviceId,
                GetClientIpAddress(),
                GetUserAgent(),
                cancellationToken);

            if (rotated == null)
            {
                return Unauthorized(new { error = "Session expired", code = "SESSION_EXPIRED" });
            }

            var sessionRow = rotated.Value.Session;
            var newRefreshPlain = rotated.Value.PlainRefreshToken;
            var userId = sessionRow.UserId;
            var user = await _rbacUserService.GetByIdAsync(userId);
            if (user == null || !user.IsActive || user.IsLocked)
            {
                return Unauthorized(new { error = "Session expired", code = "SESSION_EXPIRED" });
            }

            var allowedHotelIds = (await _hotelAccessService.GetAllowedTenantIdsAsync(userId)).ToList();
            if (allowedHotelIds.Count == 0)
            {
                return Unauthorized(new { error = "لم يُعيَّن أي فندق لهذا المستخدم", code = "SESSION_EXPIRED" });
            }

            var loginLike = new LoginRequestDto
            {
                HotelCode = request.HotelCode,
                TenantId = request.TenantId
            };
            var tenant = await ResolveRequestedTenantAsync(loginLike, allowedHotelIds[0]);
            var tenantId = tenant?.Id ?? allowedHotelIds[0];
            if (!allowedHotelIds.Contains(tenantId))
            {
                tenantId = allowedHotelIds[0];
                tenant = await _masterDbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
            }

            var authMode = await _authModeResolver.ResolveForTenantAsync(tenantId);
            var rolesList = (await _rbacUserService.GetUserRoleCodesAsync(userId)).ToList();
            var permissions = (await _permissionService.GetEffectivePermissionsAsync(userId, tenantId, authMode, cancellationToken)).ToList();
            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(fullName))
            {
                fullName = user.Username;
            }

            var token = _jwtService.GenerateToken(new JwtTokenDescriptor
            {
                UserId = userId,
                Username = user.Username,
                TenantId = tenantId,
                TenantCode = tenant?.Code ?? "",
                AuthMode = authMode,
                Roles = rolesList,
                Permissions = permissions,
                AllowedHotelIds = allowedHotelIds,
                AllowedGroupIds = new List<int>(),
                SessionVersion = user.SessionVersion,
                SessionId = sessionRow.SessionId
            });

            var sessionEntity = await _masterDbContext.PmsUserSessions.AsNoTracking()
                .FirstOrDefaultAsync(s => s.SessionId == sessionRow.SessionId, cancellationToken);

            var response = new LoginResponseDto
            {
                Token = token,
                RefreshToken = newRefreshPlain,
                RefreshExpiresAt = sessionEntity?.ExpiresAt,
                UserId = userId,
                Username = user.Username,
                FullName = fullName,
                FullNameEn = LocalizedDisplayNameHelper.UserFullNameEn(user),
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                EmployeeNumber = user.EmployeeNumber,
                TenantId = tenantId,
                TenantCode = tenant?.Code ?? "",
                TenantName = tenant?.Name ?? "",
                TenantNameEn = tenant == null ? null : LocalizedDisplayNameHelper.TenantNameEn(tenant),
                Roles = rolesList,
                Permissions = permissions,
                AllowedHotelIds = allowedHotelIds,
                AllowedGroupIds = new List<int>(),
                AuthMode = authMode,
                ExpiresAt = KsaTime.Now.AddMinutes(_jwtService.AccessTokenMinutes)
            };

            await ApplyGateLandingAsync(response, cancellationToken);

            await _sessionService.LogSecurityEventAsync(
                SecurityAuditEventTypes.Refresh,
                userId,
                userId,
                sessionRow.SessionId,
                GetClientIpAddress(),
                null,
                cancellationToken);

            return Ok(response);
        }

        /// <summary>
        /// Revokes the current session (server-side logout).
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Logout(CancellationToken cancellationToken)
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            if (!int.TryParse(userIdClaim, out var userId) || userId <= 0)
            {
                return Ok(new { success = true });
            }

            var sessionContext = await ResolveSessionContextAsync(userId);
            if (sessionContext.SessionId.HasValue)
            {
                await _sessionService.RevokeSessionAsync(
                    sessionContext.SessionId.Value,
                    userId,
                    SecurityAuditEventTypes.Logout,
                    userId,
                    cancellationToken);
            }
            else
            {
                await _sessionService.LogSecurityEventAsync(
                    SecurityAuditEventTypes.Logout,
                    userId,
                    userId,
                    null,
                    GetClientIpAddress(),
                    "Legacy token without session id",
                    cancellationToken);
            }

            return Ok(new { success = true });
        }

        /// <summary>
        /// Revokes all sessions for the signed-in user (logout all devices).
        /// </summary>
        [HttpPost("logout-all")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            if (!int.TryParse(userIdClaim, out var userId) || userId <= 0)
            {
                return Unauthorized(new { error = "Authentication required" });
            }

            await _sessionService.RevokeAllUserSessionsAsync(userId, "UserLogoutAll", userId, cancellationToken);
            return Ok(new { success = true });
        }

        private async Task ApplyGateLandingAsync(LoginResponseDto response, CancellationToken cancellationToken)
        {
            var stations = await _gateLandingService.GetUserGateStationsAsync(
                response.UserId,
                response.TenantId,
                response.Permissions,
                cancellationToken);
            response.GateStations = stations.ToList();
            response.LandingUrl = _gateLandingService.ResolvePreferredLandingUrl(stations);
        }

        /// <summary>
        /// التحقق من صحة Token (للاختبار)
        /// </summary>
        [HttpPost("validate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult ValidateToken()
        {
            var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            
            if (string.IsNullOrWhiteSpace(token))
            {
                return Unauthorized(new { error = "No token provided" });
            }

            var principal = _jwtService.ValidateToken(token);
            
            if (principal == null)
            {
                return Unauthorized(new { error = "Invalid or expired token" });
            }

            var userId = principal.FindFirst("userId")?.Value;
            var tenantId = principal.FindFirst("tenantId")?.Value;
            var username = principal.FindFirst("username")?.Value;
            var roles = principal.FindFirst("roles")?.Value;
            var permissions = principal.FindFirst("permissions")?.Value;
            var allowedHotelIds = principal.FindFirst("allowedHotelIds")?.Value;
            var allowedGroupIds = principal.FindFirst("allowedGroupIds")?.Value;
            var authMode = principal.FindFirst("authMode")?.Value;

            return Ok(new
            {
                valid = true,
                userId,
                tenantId,
                username,
                authMode,
                roles = roles?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>(),
                permissions = permissions?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>(),
                allowedHotelIds = allowedHotelIds?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>(),
                allowedGroupIds = allowedGroupIds?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>()
            });
        }

        /// <summary>
        /// إنشاء Password Hash آمن (للاختبار فقط - Development)
        /// </summary>
        [HttpPost("generate-hash")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GeneratePasswordHash([FromBody] GenerateHashRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { error = "Password is required" });
            }

            try
            {
                var hash = _rbacUserService.HashPassword(request.Password);

                _logger.LogInformation("✅ Password hash generated for user: {Username}", request.Username ?? "N/A");

                return Ok(new
                {
                    hash,
                    note = "PBKDF2 hash generated. Do not log or store plain text passwords.",
                    sqlUpdate = $"UPDATE users SET password_hash = '{hash}' WHERE username = '{request.Username ?? "user1"}';"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating password");
                return StatusCode(500, new { error = "Error generating password", message = ex.Message });
            }
        }

        /// <summary>
        /// الحصول على مفتاح ترخيص DevExtreme (محمي - يتطلب تسجيل الدخول)
        /// Get DevExtreme License Key (Protected - Requires Authentication)
        /// </summary>
        [HttpGet("devextreme-license")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult GetDevExtremeLicense()
        {
            // التحقق من وجود Token في Header
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                _logger.LogWarning("❌ DevExtreme license request without authentication");
                return Unauthorized(new { error = "Authentication required" });
            }

            var token = authHeader.Replace("Bearer ", "");
            var principal = _jwtService.ValidateToken(token);
            
            if (principal == null)
            {
                _logger.LogWarning("❌ DevExtreme license request with invalid token");
                return Unauthorized(new { error = "Invalid or expired token" });
            }

            // ✅ Token صالح - إرجاع مفتاح الترخيص من appsettings.json فقط
            // License key is stored in appsettings.json for security - no hardcoded fallback
            var licenseKey = _configuration["DevExtreme:LicenseKey"];
            
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                _logger.LogError("❌ DevExtreme license key not found in appsettings.json");
                return StatusCode(500, new { error = "DevExtreme license key is not configured" });
            }

            var username = principal.FindFirst("username")?.Value ?? "Unknown";
            _logger.LogInformation("✅ DevExtreme license key provided to authenticated user: {Username}", username);

            return Ok(new { licenseKey });
        }

        /// <summary>
        /// طلب إعادة تعيين كلمة المرور
        /// ✅ Senior Level: Production-ready password reset with email verification
        /// </summary>
        /// <param name="request">بيانات الطلب (اسم المستخدم أو البريد الإلكتروني)</param>
        /// <returns>رسالة نجاح (دائماً لإخفاء وجود المستخدم من عدمه)</returns>
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { error = "Invalid request", details = ModelState });
            }

            try
            {
                // ✅ Get IP address for security logging
                var ipAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

                // ✅ Find user by EmployeeNumber or Email only (for password reset)
                MasterRbacUser? user = await _rbacUserService.GetByEmployeeNumberAsync(request.UsernameOrEmail);
                if (user == null)
                {
                    user = await _rbacUserService.GetByEmailAsync(request.UsernameOrEmail);
                }

                // ✅ Security: Always return success message (don't reveal if user exists)
                // This prevents user enumeration attacks
                var successMessage = "إذا كان الحساب موجوداً ومرتبطاً ببريد إلكتروني أو رقم جوال، سيتم إرسال رابط إعادة التعيين.";

                if (user == null)
                {
                    _logger.LogWarning("⚠️ ForgotPassword: User not found. UsernameOrEmail={UsernameOrEmail}, IP={IP}",
                        request.UsernameOrEmail, ipAddress);
                    // Still return success for security
                    return Ok(new { message = successMessage });
                }

                // ✅ Check if user has email or phone number
                var hasEmail = !string.IsNullOrWhiteSpace(user.Email);
                var hasPhone = !string.IsNullOrWhiteSpace(user.PhoneNumber);

                if (!hasEmail && !hasPhone)
                {
                    _logger.LogWarning("⚠️ ForgotPassword: User has no email or phone. UserId={UserId}, EmployeeNumber={EmployeeNumber}, Username={Username}, IP={IP}",
                        user.UserId, user.EmployeeNumber ?? "N/A", user.Username, ipAddress);
                    // Still return success for security
                    return Ok(new { message = successMessage });
                }

                // ✅ Check if user is active
                if (!user.IsActive)
                {
                    _logger.LogWarning("⚠️ ForgotPassword: User is inactive. UserId={UserId}, EmployeeNumber={EmployeeNumber}, Username={Username}, IP={IP}",
                        user.UserId, user.EmployeeNumber ?? "N/A", user.Username, ipAddress);
                    // Still return success for security
                    return Ok(new { message = successMessage });
                }

                // ✅ Rate limiting: Check recent reset requests (prevent abuse)
                // In production, you might want to implement more sophisticated rate limiting
                var recentRequests = await _rbacUserService.GetRecentResetRequestsAsync(user.UserId, TimeSpan.FromMinutes(5));
                if (recentRequests >= 3)
                {
                    _logger.LogWarning("⚠️ ForgotPassword: Too many reset requests. UserId={UserId}, IP={IP}",
                        user.UserId, ipAddress);
                    // Still return success for security
                    return Ok(new { message = successMessage });
                }

                // ✅ Create reset token
                var resetToken = await _rbacUserService.CreatePasswordResetTokenAsync(user.UserId, ipAddress);

                // ✅ Generate reset URL
                var baseUrl = _configuration["AppSettings:ApprovalBaseUrl"] 
                    ?? _httpContextAccessor.HttpContext?.Request.Scheme + "://" + _httpContextAccessor.HttpContext?.Request.Host;
                var resetUrl = $"{baseUrl}/reset-password.html?token={Uri.EscapeDataString(resetToken)}";

                var userName = $"{user.FirstName} {user.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(userName))
                {
                    userName = user.Username;
                }
                var emailSent = false;
                var whatsAppSent = false;

                // ✅ Send email if user has email
                if (hasEmail)
                {
                    emailSent = await _emailService.SendPasswordResetEmailAsync(
                        user.Email!,
                        userName,
                        resetToken,
                        resetUrl
                    );

                    if (emailSent)
                    {
                        _logger.LogInformation("✅ ForgotPassword: Reset email sent. UserId={UserId}, Email={Email}, IP={IP}",
                            user.UserId, user.Email, ipAddress);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ ForgotPassword: Failed to send email. UserId={UserId}, Email={Email}, IP={IP}",
                            user.UserId, user.Email, ipAddress);
                    }
                }

                // ✅ Send WhatsApp if user has phone number
                if (hasPhone)
                {
                    whatsAppSent = await SendPasswordResetWhatsAppAsync(
                        user.PhoneNumber!,
                        userName,
                        resetUrl
                    );

                    if (whatsAppSent)
                    {
                        _logger.LogInformation("✅ ForgotPassword: Reset WhatsApp sent. UserId={UserId}, Phone={Phone}, IP={IP}",
                            user.UserId, user.PhoneNumber, ipAddress);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ ForgotPassword: Failed to send WhatsApp. UserId={UserId}, Phone={Phone}, IP={IP}",
                            user.UserId, user.PhoneNumber, ipAddress);
                    }
                }

                // ✅ Log summary
                if (emailSent || whatsAppSent)
                {
                    _logger.LogInformation("✅ ForgotPassword: Reset link sent successfully. UserId={UserId}, Email={EmailSent}, WhatsApp={WhatsAppSent}",
                        user.UserId, emailSent, whatsAppSent);
                }

                // ✅ Always return success (security best practice)
                return Ok(new { message = successMessage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ForgotPassword: Error processing request. UsernameOrEmail={UsernameOrEmail}", 
                    request.UsernameOrEmail);
                // Still return success for security
                return Ok(new { message = "إذا كان الحساب موجوداً ومرتبطاً ببريد إلكتروني أو رقم جوال، سيتم إرسال رابط إعادة التعيين." });
            }
        }

        /// <summary>
        /// إرسال رابط إعادة تعيين كلمة المرور عبر WhatsApp
        /// </summary>
        private async Task<bool> SendPasswordResetWhatsAppAsync(string phoneNumber, string userName, string resetUrl)
        {
            try
            {
                // Format WhatsApp message in Arabic
                var message = $@"🔐 *إعادة تعيين كلمة المرور - فنادق العييري*

مرحباً {userName}،

لقد تلقينا طلباً لإعادة تعيين كلمة المرور لحسابك في نظام تتبع المصروفات.

يمكنك إعادة تعيين كلمة المرور من خلال النقر على الرابط التالي:

{resetUrl}

⚠️ *تحذيرات مهمة:*
• هذا الرابط صالح لمدة *30 دقيقة* فقط
• إذا لم تطلب إعادة التعيين، يرجى تجاهل هذه الرسالة
• لا تشارك هذا الرابط مع أي شخص آخر

© {KsaTime.Now.Year} فنادق العييري - جميع الحقوق محفوظة";

                // WhatsAppService expects phone number without 966 prefix
                // It will handle the removal if present, but we ensure it's in the right format
                var cleanPhoneNumber = phoneNumber.Trim();
                if (cleanPhoneNumber.StartsWith("966"))
                {
                    cleanPhoneNumber = cleanPhoneNumber.Substring(3);
                }

                // Remove any non-digit characters (spaces, dashes, etc.)
                cleanPhoneNumber = new string(cleanPhoneNumber.Where(char.IsDigit).ToArray());

                if (string.IsNullOrWhiteSpace(cleanPhoneNumber))
                {
                    _logger.LogWarning("⚠️ SendPasswordResetWhatsApp: Invalid phone number format. Phone={Phone}", phoneNumber);
                    return false;
                }

                var (success, errorMessage) = await _whatsAppService.SendMessageAsync(cleanPhoneNumber, message);

                if (success)
                {
                    _logger.LogInformation("✅ SendPasswordResetWhatsApp: Message sent successfully. Phone={Phone}", cleanPhoneNumber);
                    return true;
                }
                else
                {
                    _logger.LogWarning("⚠️ SendPasswordResetWhatsApp: Failed to send. Phone={Phone}, Error={Error}", 
                        cleanPhoneNumber, errorMessage);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ SendPasswordResetWhatsApp: Error sending WhatsApp message. Phone={Phone}", phoneNumber);
                return false;
            }
        }

        private async Task<Tenant?> ResolveRequestedTenantAsync(LoginRequestDto request, int fallbackTenantId)
        {
            if (request.TenantId.HasValue && request.TenantId.Value > 0)
            {
                return await _masterDbContext.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == request.TenantId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.HotelCode))
            {
                var hotelCode = request.HotelCode.Trim();
                return await _masterDbContext.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Code == hotelCode);
            }

            return await _masterDbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == fallbackTenantId);
        }

        private long? TryGetCurrentSessionId()
        {
            var sidClaim = User.FindFirst("sid")?.Value;
            if (long.TryParse(sidClaim, out var parsedSessionId) && parsedSessionId > 0)
            {
                return parsedSessionId;
            }

            return null;
        }

        private async Task<(int SessionVersion, long? SessionId)> ResolveSessionContextAsync(int userId)
        {
            var sessionId = TryGetCurrentSessionId();
            var authState = await _sessionService.GetUserAuthStateAsync(userId);
            return (authState?.SessionVersion ?? 0, sessionId);
        }

        private string? GetClientIpAddress()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                return null;
            }

            var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                return forwarded.Split(',')[0].Trim();
            }

            return context.Connection.RemoteIpAddress?.ToString();
        }

        private string? GetUserAgent()
        {
            var value = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        /// <summary>
        /// إعادة تعيين كلمة المرور باستخدام الرمز المميز
        /// ✅ Senior Level: Secure password reset with token validation
        /// </summary>
        /// <param name="request">بيانات إعادة التعيين</param>
        /// <returns>رسالة نجاح</returns>
        [HttpPost("reset-password")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { error = "Invalid request", details = ModelState });
            }

            try
            {
                // ✅ Validate token
                var userId = await _rbacUserService.ValidateResetTokenAsync(request.Token);
                if (!userId.HasValue)
                {
                    _logger.LogWarning("❌ ResetPassword: Invalid or expired token");
                    return Unauthorized(new { error = "الرمز المميز غير صالح أو منتهي الصلاحية. يرجى طلب رابط جديد." });
                }

                // ✅ Reset password
                var success = await _rbacUserService.ResetPasswordAsync(request.Token, request.NewPassword);

                if (success)
                {
                    _logger.LogInformation("✅ ResetPassword: Password reset successfully. UserId={UserId}", userId.Value);
                    return Ok(new { message = "تم إعادة تعيين كلمة المرور بنجاح. يمكنك الآن تسجيل الدخول بكلمة المرور الجديدة." });
                }
                else
                {
                    _logger.LogWarning("❌ ResetPassword: Failed to reset password. UserId={UserId}", userId.Value);
                    return BadRequest(new { error = "فشل إعادة تعيين كلمة المرور. يرجى المحاولة مرة أخرى." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ResetPassword: Error resetting password");
                return StatusCode(500, new { error = "حدث خطأ أثناء إعادة تعيين كلمة المرور. يرجى المحاولة مرة أخرى." });
            }
        }
    }

    /// <summary>
    /// DTO لطلب إنشاء Hash
    /// </summary>
    public class GenerateHashRequestDto
    {
        /// <summary>
        /// Plain password that will be converted to a secure hash.
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Optional username used only to prepare a helper SQL update statement.
        /// </summary>
        public string? Username { get; set; }
    }
}

