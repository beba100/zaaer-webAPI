using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.Services.Integrations.Zatca;

namespace zaaerIntegration.Services.Integrations
{
    /// <summary>
    /// Central checks for per-hotel integration enablement (<c>is_active</c> on *_details tables).
    /// </summary>
    public static class IntegrationPlatformGuard
    {
        public const string ZatcaInactiveMessage =
            "ZATCA integration is inactive for this hotel. Enable it in ZATCA settings.";

        public const string NtmpInactiveMessage =
            "NTMP integration is inactive for this hotel. Enable it in NTMP settings.";

        public const string ShomoosInactiveMessage =
            "Shomoos integration is inactive for this hotel. Enable it in Shomoos settings.";

        public static async Task<ZatcaDetails?> LoadActiveZatcaForHotelAsync(
            ApplicationDbContext db,
            int hotelId,
            CancellationToken cancellationToken = default)
        {
            var details = await ZatcaDetailsEnvironmentSync.LoadAlignedForHotelAsync(db, hotelId, cancellationToken);
            if (details == null || !details.IsActive)
            {
                return null;
            }

            return details;
        }

        public static async Task<ZatcaDetails?> LoadActiveZatcaAsync(
            ApplicationDbContext db,
            CancellationToken cancellationToken = default)
        {
            var details = await ZatcaDetailsEnvironmentSync.LoadAlignedAsync(db, cancellationToken);
            if (details == null || !details.IsActive)
            {
                return null;
            }

            return details;
        }

        public static async Task<bool> IsNtmpActiveForHotelAsync(
            ApplicationDbContext db,
            int hotelId,
            CancellationToken cancellationToken = default)
        {
            var settings = await db.NtmpDetails.AsNoTracking()
                .Where(n => n.IsActive && (n.HotelId == hotelId || n.ZaaerId == hotelId))
                .AnyAsync(cancellationToken);
            if (settings)
            {
                return true;
            }

            var zaaerFromInternal = await db.HotelSettings.AsNoTracking()
                .Where(h => h.HotelId == hotelId && h.ZaaerId != null)
                .Select(h => h.ZaaerId!.Value)
                .FirstOrDefaultAsync(cancellationToken);
            if (zaaerFromInternal == 0)
            {
                return false;
            }

            return await db.NtmpDetails.AsNoTracking()
                .AnyAsync(
                    n => n.IsActive && (n.HotelId == zaaerFromInternal || n.ZaaerId == zaaerFromInternal),
                    cancellationToken);
        }

        public static async Task<bool> IsShomoosActiveForHotelAsync(
            ApplicationDbContext db,
            int hotelId,
            CancellationToken cancellationToken = default)
        {
            return await db.ShomoosDetails.AsNoTracking()
                .AnyAsync(s => s.HotelId == hotelId && s.IsActive, cancellationToken);
        }
    }
}
