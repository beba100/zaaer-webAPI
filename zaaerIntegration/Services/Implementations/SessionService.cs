using System.Security.Cryptography;
using System.Text;
using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Auth;
using zaaerIntegration.Services.Auth;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    public class SessionService : ISessionService
    {
        private const string UserAuthCachePrefix = "auth:user:";
        private static readonly TimeSpan UserAuthCacheDuration = TimeSpan.FromSeconds(45);
        private static readonly TimeSpan SessionActiveCacheDuration = TimeSpan.FromSeconds(30);

        private readonly MasterDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SessionService> _logger;

        public SessionService(
            MasterDbContext db,
            IMemoryCache cache,
            IConfiguration configuration,
            ILogger<SessionService> logger)
        {
            _db = db;
            _cache = cache;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<(string PlainRefreshToken, long SessionId, DateTime ExpiresAt)> CreateSessionAsync(
            int userId,
            string? deviceId,
            string? deviceName,
            string? ipAddress,
            string? userAgent,
            CancellationToken cancellationToken = default)
        {
            var plain = GenerateRefreshToken();
            var hash = HashRefreshToken(plain);
            var now = KsaTime.Now;
            var expiresAt = now.AddDays(GetRefreshTokenDays());

            var session = new PmsUserSession
            {
                UserId = userId,
                RefreshTokenHash = hash,
                DeviceId = TrimTo(deviceId, 100),
                DeviceName = TrimTo(deviceName, 200),
                IpAddress = TrimTo(ipAddress, 64),
                UserAgent = TrimTo(userAgent, 500),
                CreatedAt = now,
                ExpiresAt = expiresAt,
                LastActivityAt = now
            };

            _db.PmsUserSessions.Add(session);
            await _db.SaveChangesAsync(cancellationToken);

            _cache.Set(SessionCacheKey(session.SessionId), true, SessionActiveCacheDuration);
            return (plain, session.SessionId, expiresAt);
        }

        public async Task<(PmsUserSessionRow Session, string PlainRefreshToken)?> RotateRefreshTokenAsync(
            string plainRefreshToken,
            string? deviceId,
            string? ipAddress,
            string? userAgent,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(plainRefreshToken))
            {
                return null;
            }

            var hash = HashRefreshToken(plainRefreshToken);
            var now = KsaTime.Now;

            var session = await _db.PmsUserSessions
                .FirstOrDefaultAsync(
                    s => s.RefreshTokenHash == hash
                         && s.RevokedAt == null
                         && s.ExpiresAt > now,
                    cancellationToken);

            if (session == null)
            {
                return null;
            }

            var authState = await GetUserAuthStateAsync(session.UserId, cancellationToken);
            if (authState == null || !authState.IsActive || authState.IsLocked)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(deviceId)
                && !string.IsNullOrWhiteSpace(session.DeviceId)
                && !string.Equals(session.DeviceId, deviceId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Refresh token device mismatch for user {UserId}, session {SessionId}",
                    session.UserId,
                    session.SessionId);
                return null;
            }

            var newPlain = GenerateRefreshToken();
            session.RefreshTokenHash = HashRefreshToken(newPlain);
            session.LastActivityAt = now;
            session.IpAddress = TrimTo(ipAddress, 64) ?? session.IpAddress;
            session.UserAgent = TrimTo(userAgent, 500) ?? session.UserAgent;
            session.ExpiresAt = now.AddDays(GetRefreshTokenDays());

            await _db.SaveChangesAsync(cancellationToken);
            _cache.Set(SessionCacheKey(session.SessionId), true, SessionActiveCacheDuration);

            return (new PmsUserSessionRow { SessionId = session.SessionId, UserId = session.UserId }, newPlain);
        }

        public async Task<bool> RevokeSessionAsync(
            long sessionId,
            int userId,
            string reason,
            int? actorUserId,
            CancellationToken cancellationToken = default)
        {
            var session = await _db.PmsUserSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.UserId == userId && s.RevokedAt == null, cancellationToken);

            if (session == null)
            {
                return false;
            }

            session.RevokedAt = KsaTime.Now;
            session.RevokedBy = actorUserId;
            session.RevokeReason = TrimTo(reason, 200);
            await _db.SaveChangesAsync(cancellationToken);

            _cache.Remove(SessionCacheKey(sessionId));
            await LogSecurityEventAsync(
                SecurityAuditEventTypes.SessionRevoked,
                userId,
                actorUserId,
                sessionId,
                session.IpAddress,
                reason,
                cancellationToken);

            return true;
        }

        public async Task RevokeAllUserSessionsAsync(
            int userId,
            string reason,
            int? actorUserId,
            CancellationToken cancellationToken = default)
        {
            var now = KsaTime.Now;

            await _db.PmsUserSessions
                .Where(s => s.UserId == userId && s.RevokedAt == null)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(x => x.RevokedAt, now)
                        .SetProperty(x => x.RevokedBy, actorUserId)
                        .SetProperty(x => x.RevokeReason, TrimTo(reason, 200)),
                    cancellationToken);

            await _db.RbacUsers
                .Where(u => u.UserId == userId)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(u => u.SessionVersion, u => u.SessionVersion + 1)
                        .SetProperty(u => u.UpdatedAt, now),
                    cancellationToken);

            InvalidateUserAuthCache(userId);

            await LogSecurityEventAsync(
                SecurityAuditEventTypes.ForceLogout,
                userId,
                actorUserId,
                null,
                null,
                reason,
                cancellationToken);
        }

        public async Task<IReadOnlyList<UserSessionDto>> GetUserSessionsForAdminAsync(
            int userId,
            long? currentSessionId = null,
            CancellationToken cancellationToken = default)
        {
            var now = KsaTime.Now;

            return await _db.PmsUserSessions
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.LastActivityAt)
                .Take(50)
                .Select(s => new UserSessionDto
                {
                    SessionId = s.SessionId,
                    DeviceId = s.DeviceId,
                    DeviceName = s.DeviceName,
                    IpAddress = s.IpAddress,
                    CreatedAt = s.CreatedAt,
                    LastActivityAt = s.LastActivityAt,
                    ExpiresAt = s.ExpiresAt,
                    RevokedAt = s.RevokedAt,
                    IsActive = s.RevokedAt == null && s.ExpiresAt > now,
                    Status = s.RevokedAt != null
                        ? "Revoked"
                        : (s.ExpiresAt <= now ? "Expired" : "Active"),
                    IsCurrent = currentSessionId.HasValue && s.SessionId == currentSessionId.Value
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<long?> TouchSessionActivityAsync(
            long sessionId,
            string? ipAddress,
            CancellationToken cancellationToken = default)
        {
            var now = KsaTime.Now;
            var session = await _db.PmsUserSessions
                .FirstOrDefaultAsync(
                    s => s.SessionId == sessionId && s.RevokedAt == null && s.ExpiresAt > now,
                    cancellationToken);

            if (session == null)
            {
                return null;
            }

            session.LastActivityAt = now;
            if (!string.IsNullOrWhiteSpace(ipAddress))
            {
                session.IpAddress = TrimTo(ipAddress, 64);
            }

            await _db.SaveChangesAsync(cancellationToken);
            _cache.Set(SessionCacheKey(sessionId), true, SessionActiveCacheDuration);
            return session.SessionId;
        }

        public async Task<long> EnsureActiveSessionAsync(
            int userId,
            long? existingSessionId,
            string? deviceId,
            string? deviceName,
            string? ipAddress,
            string? userAgent,
            CancellationToken cancellationToken = default)
        {
            if (existingSessionId.HasValue)
            {
                var touched = await TouchSessionActivityAsync(existingSessionId.Value, ipAddress, cancellationToken);
                if (touched.HasValue)
                {
                    return touched.Value;
                }
            }

            var (_, sessionId, _) = await CreateSessionAsync(
                userId,
                deviceId,
                deviceName,
                ipAddress,
                userAgent,
                cancellationToken);

            return sessionId;
        }

        public async Task<UserAuthState?> GetUserAuthStateAsync(int userId, CancellationToken cancellationToken = default)
        {
            var cacheKey = UserAuthCacheKey(userId);
            if (_cache.TryGetValue(cacheKey, out UserAuthState? cached) && cached != null)
            {
                return cached;
            }

            var row = await _db.RbacUsers
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new UserAuthState
                {
                    UserId = u.UserId,
                    IsActive = u.IsActive && u.Status,
                    IsLocked = u.IsLocked,
                    SessionVersion = u.SessionVersion
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (row != null)
            {
                _cache.Set(cacheKey, row, UserAuthCacheDuration);
            }

            return row;
        }

        public async Task<bool> IsSessionActiveAsync(long sessionId, CancellationToken cancellationToken = default)
        {
            var cacheKey = SessionCacheKey(sessionId);
            if (_cache.TryGetValue(cacheKey, out bool isActive))
            {
                return isActive;
            }

            var now = KsaTime.Now;
            var active = await _db.PmsUserSessions
                .AsNoTracking()
                .AnyAsync(s => s.SessionId == sessionId && s.RevokedAt == null && s.ExpiresAt > now, cancellationToken);

            _cache.Set(cacheKey, active, SessionActiveCacheDuration);
            return active;
        }

        public async Task LogSecurityEventAsync(
            string eventType,
            int? userId,
            int? actorUserId,
            long? sessionId,
            string? ipAddress,
            string? details,
            CancellationToken cancellationToken = default)
        {
            _db.PmsSecurityAudits.Add(new PmsSecurityAudit
            {
                EventType = eventType,
                UserId = userId,
                ActorUserId = actorUserId,
                SessionId = sessionId,
                IpAddress = TrimTo(ipAddress, 64),
                Details = details,
                CreatedAt = KsaTime.Now
            });

            await _db.SaveChangesAsync(cancellationToken);
        }

        public void InvalidateUserAuthCache(int userId)
        {
            _cache.Remove(UserAuthCacheKey(userId));
        }

        internal static string GenerateRefreshToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(48);
            return Convert.ToBase64String(bytes);
        }

        internal static string HashRefreshToken(string plain)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plain));
            return Convert.ToHexString(bytes);
        }

        private int GetRefreshTokenDays()
        {
            return _configuration.GetValue("Jwt:RefreshTokenDays", 30);
        }

        private static string? TrimTo(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }

        private static string UserAuthCacheKey(int userId) => $"{UserAuthCachePrefix}{userId}";
        private static string SessionCacheKey(long sessionId) => $"auth:session:{sessionId}";
    }
}
