using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Services.Implementations
{
    public class HotelAccessService : IHotelAccessService
    {
        private readonly MasterDbContext _masterDbContext;
        private readonly ILogger<HotelAccessService> _logger;

        public HotelAccessService(MasterDbContext masterDbContext, ILogger<HotelAccessService> logger)
        {
            _masterDbContext = masterDbContext;
            _logger = logger;
        }

        public async Task<IReadOnlyCollection<int>> GetAllowedTenantIdsAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _masterDbContext.PmsUserHotels
                    .AsNoTracking()
                    .Where(x => x.IsActive && x.UserId == userId && x.TenantId > 0)
                    .Select(x => x.TenantId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SECURITY] PMS hotel access lookup failed for UserId {UserId}", userId);
                return Array.Empty<int>();
            }
        }

        public async Task<bool> CanAccessTenantAsync(int userId, int tenantId, CancellationToken cancellationToken = default)
        {
            var allowed = await GetAllowedTenantIdsAsync(userId, cancellationToken);
            return allowed.Contains(tenantId);
        }
    }
}
