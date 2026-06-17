using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Services.BookingEngine
{
    /// <summary>
    /// Resolves tenant by hotel code and opens per-tenant <see cref="ApplicationDbContext"/>.
    /// </summary>
    public sealed class BookingEngineDbFactory
    {
        private readonly MasterDbContext _masterDbContext;
        private readonly ITenantService _tenantService;

        public BookingEngineDbFactory(MasterDbContext masterDbContext, ITenantService tenantService)
        {
            _masterDbContext = masterDbContext;
            _tenantService = tenantService;
        }

        public async Task<Tenant?> ResolveTenantByCodeAsync(string hotelCode, CancellationToken cancellationToken = default)
        {
            var code = (hotelCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            return await _masterDbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    t => t.Code != null && t.Code.ToLower() == code.ToLower(),
                    cancellationToken);
        }

        public async Task<Tenant?> ResolveTenantBySlugAsync(string slug, CancellationToken cancellationToken = default)
        {
            var s = (slug ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(s))
            {
                return null;
            }

            var tenants = await _masterDbContext.Tenants
                .AsNoTracking()
                .Where(t => t.DatabaseName != null && t.DatabaseName.Trim() != "" && t.Code != null)
                .ToListAsync(cancellationToken);

            foreach (var tenant in tenants)
            {
                await using var ctx = CreateContext(tenant);
                var hotel = await ctx.HotelSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
                if (hotel == null)
                {
                    continue;
                }

                var settings = await ctx.BookingEngineSettings.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.HotelId == hotel.HotelId, cancellationToken);

                var settingsSlug = (settings?.PublicSlug ?? string.Empty).Trim().ToLowerInvariant();
                var codeSlug = (tenant.Code ?? string.Empty).Trim().ToLowerInvariant();
                var hotelCodeSlug = (hotel.HotelCode ?? string.Empty).Trim().ToLowerInvariant();

                if (settingsSlug == s || codeSlug == s || hotelCodeSlug == s)
                {
                    return tenant;
                }
            }

            return null;
        }

        public ApplicationDbContext CreateContext(Tenant tenant)
        {
            var connectionString = _tenantService.BuildConnectionStringForTenant(tenant);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            return new ApplicationDbContext(options);
        }
    }
}
