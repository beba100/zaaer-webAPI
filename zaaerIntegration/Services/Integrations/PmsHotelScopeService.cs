using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Services.Integrations
{
    public abstract class PmsHotelScopeService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITenantService _tenantService;

        protected PmsHotelScopeService(ApplicationDbContext context, ITenantService tenantService)
        {
            _context = context;
            _tenantService = tenantService;
        }

        protected async Task<HotelSettings> GetCurrentHotelSettingsAsync(CancellationToken cancellationToken = default)
        {
            var tenant = _tenantService.GetTenant()
                ?? throw new UnauthorizedAccessException("Tenant not resolved. Provide X-Hotel-Code.");

            var code = tenant.Code.Trim();
            var hotel = await _context.HotelSettings.AsNoTracking()
                .Where(h => h.HotelCode != null)
                .FirstOrDefaultAsync(
                    h => h.HotelCode!.ToLower() == code.ToLower(),
                    cancellationToken)
                ?? throw new InvalidOperationException($"HotelSettings not found for code: {tenant.Code}");

            return hotel;
        }

        /// <summary>
        /// Zaaer property id (<c>hotel_settings.zaaer_id</c>) — used as <c>hotel_id</c> on integration tables
        /// (same convention as expenses, taxes, partner queue).
        /// </summary>
        protected async Task<int> GetCurrentHotelZaaerIdAsync(CancellationToken cancellationToken = default)
        {
            var hotel = await GetCurrentHotelSettingsAsync(cancellationToken);
            if (!hotel.ZaaerId.HasValue)
            {
                throw new InvalidOperationException(
                    $"ZaaerId is not configured for hotel code: {hotel.HotelCode}. Set hotel_settings.zaaer_id.");
            }

            return hotel.ZaaerId.Value;
        }

        /// <inheritdoc cref="GetCurrentHotelZaaerIdAsync"/>
        protected Task<int> GetCurrentHotelIdAsync(CancellationToken cancellationToken = default) =>
            GetCurrentHotelZaaerIdAsync(cancellationToken);
    }
}
