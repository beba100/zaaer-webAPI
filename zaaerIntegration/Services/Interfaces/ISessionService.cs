using zaaerIntegration.DTOs.Auth;

namespace zaaerIntegration.Services.Interfaces
{
    public interface ISessionService
    {
        Task<(string PlainRefreshToken, long SessionId, DateTime ExpiresAt)> CreateSessionAsync(
            int userId,
            string? deviceId,
            string? deviceName,
            string? ipAddress,
            string? userAgent,
            CancellationToken cancellationToken = default);

        Task<(PmsUserSessionRow Session, string PlainRefreshToken)?> RotateRefreshTokenAsync(
            string plainRefreshToken,
            string? deviceId,
            string? ipAddress,
            string? userAgent,
            CancellationToken cancellationToken = default);

        Task<bool> RevokeSessionAsync(
            long sessionId,
            int userId,
            string reason,
            int? actorUserId,
            CancellationToken cancellationToken = default);

        Task RevokeAllUserSessionsAsync(
            int userId,
            string reason,
            int? actorUserId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<UserSessionDto>> GetUserSessionsForAdminAsync(
            int userId,
            long? currentSessionId = null,
            CancellationToken cancellationToken = default);

        Task<long?> TouchSessionActivityAsync(
            long sessionId,
            string? ipAddress,
            CancellationToken cancellationToken = default);

        Task<long> EnsureActiveSessionAsync(
            int userId,
            long? existingSessionId,
            string? deviceId,
            string? deviceName,
            string? ipAddress,
            string? userAgent,
            CancellationToken cancellationToken = default);

        Task<UserAuthState?> GetUserAuthStateAsync(int userId, CancellationToken cancellationToken = default);

        Task<bool> IsSessionActiveAsync(long sessionId, CancellationToken cancellationToken = default);

        Task LogSecurityEventAsync(
            string eventType,
            int? userId,
            int? actorUserId,
            long? sessionId,
            string? ipAddress,
            string? details,
            CancellationToken cancellationToken = default);

        void InvalidateUserAuthCache(int userId);
    }

    public sealed class PmsUserSessionRow
    {
        public long SessionId { get; init; }
        public int UserId { get; init; }
    }

    public sealed class UserAuthState
    {
        public int UserId { get; init; }
        public bool IsActive { get; init; }
        public bool IsLocked { get; init; }
        public int SessionVersion { get; init; }
    }
}
