using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Security
{
    /// <summary>
    /// Resolves the authenticated PMS operator id (<c>pms_users.user_id</c> from JWT <c>userId</c> claim).
    /// </summary>
    public static class PmsCurrentUser
    {
        /// <summary>
        /// Prefer JWT / <see cref="ICurrentUserContext.UserId"/>; optional DTO value only when context has no user (legacy).
        /// </summary>
        public static int? ResolveUserId(ICurrentUserContext? context, int? dtoUserId = null)
        {
            if (context?.UserId is > 0)
            {
                return context.UserId.Value;
            }

            if (dtoUserId is > 0)
            {
                return dtoUserId.Value;
            }

            return null;
        }

        public static string ResolveDisplayName(ICurrentUserContext? context, string? fallbackDisplayName = null)
        {
            if (!string.IsNullOrWhiteSpace(context?.Username))
            {
                return context.Username.Trim();
            }

            if (!string.IsNullOrWhiteSpace(fallbackDisplayName))
            {
                return fallbackDisplayName.Trim();
            }

            if (context?.UserId is > 0)
            {
                return $"User #{context.UserId.Value}";
            }

            return "System";
        }
    }
}
