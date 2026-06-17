using FinanceLedgerAPI.Models;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Services
{
  /// <summary>
  /// Provides tenant query filters based on the authenticated user's hotel access.
  /// </summary>
  public static class TenantScope
  {
    /// <summary>
    /// Restricts a tenant query to the hotels explicitly assigned to the current user.
    /// </summary>
    public static IQueryable<Tenant> FilterForUser(IQueryable<Tenant> query, ICurrentUserContext user)
    {
      if (!user.IsAuthenticated)
      {
        return query.Where(_ => false);
      }

      var allowed = user.AllowedHotelIds;
      if (allowed.Count == 0)
      {
        return query.Where(_ => false);
      }

      return query.Where(t => allowed.Contains(t.Id));
    }
  }
}
