using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.Services;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Services.Implementations
{
    public class HotelScopeService : IHotelScopeService
    {
        private readonly MasterDbContext _masterDbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly IHotelAccessService _hotelAccessService;
        private readonly ILogger<HotelScopeService> _logger;

        public HotelScopeService(
            MasterDbContext masterDbContext,
            ICurrentUserContext currentUser,
            IHotelAccessService hotelAccessService,
            ILogger<HotelScopeService> logger)
        {
            _masterDbContext = masterDbContext;
            _currentUser = currentUser;
            _hotelAccessService = hotelAccessService;
            _logger = logger;
        }

        public async Task<IReadOnlyList<Tenant>> ResolveTenantsAsync(
            string? hotelCodesCsv = null,
            CancellationToken cancellationToken = default)
        {
            EnsureAuthenticated();

            var scopedQuery = TenantScope.FilterForUser(
                _masterDbContext.Tenants.AsNoTracking(),
                _currentUser);

            if (string.IsNullOrWhiteSpace(hotelCodesCsv))
            {
                return await scopedQuery.ToListAsync(cancellationToken);
            }

            var requestedCodes = hotelCodesCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (requestedCodes.Count == 0)
            {
                return await scopedQuery.ToListAsync(cancellationToken);
            }

            var tenants = await scopedQuery
                .Where(t => requestedCodes.Contains(t.Code))
                .ToListAsync(cancellationToken);

            if (tenants.Count != requestedCodes.Count)
            {
                _logger.LogWarning(
                    "[SECURITY] User {UserId} requested inaccessible hotel codes: {HotelCodes}",
                    _currentUser.UserId,
                    string.Join(", ", requestedCodes));

                throw new UnauthorizedAccessException("One or more requested hotels are not accessible.");
            }

            return tenants;
        }

        public async Task<Tenant?> FindAccessibleTenantByCodeAsync(
            string code,
            CancellationToken cancellationToken = default)
        {
            EnsureAuthenticated();

            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            var normalized = code.Trim();
            var scopedQuery = TenantScope.FilterForUser(
                _masterDbContext.Tenants.AsNoTracking(),
                _currentUser);

            return await scopedQuery
                .FirstOrDefaultAsync(t => t.Code.ToLower() == normalized.ToLower(), cancellationToken);
        }

        public async Task<bool> CanAccessTenantIdAsync(int tenantId, CancellationToken cancellationToken = default)
        {
            if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue || tenantId <= 0)
            {
                return false;
            }

            return await _hotelAccessService.CanAccessTenantAsync(
                _currentUser.UserId.Value,
                tenantId,
                cancellationToken);
        }

        private void EnsureAuthenticated()
        {
            if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
            {
                throw new UnauthorizedAccessException("Authentication is required.");
            }
        }
    }
}
