using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Integrations.Zatca
{
    /// <summary>
    /// Keeps <see cref="ZatcaDetails.ApiEnvironment"/> and <see cref="ZatcaDetails.Environment"/> aligned.
    /// <c>api_environment</c> is the source of truth when both are present.
    /// </summary>
    public static class ZatcaDetailsEnvironmentSync
    {
        public static string ResolveEffective(string? apiEnvironment, string? environment) =>
            ZatcaApiConstants.NormalizeEnvironment(
                !string.IsNullOrWhiteSpace(apiEnvironment) ? apiEnvironment : environment);

        /// <summary>Sets both columns to the same normalized value (from API env when provided).</summary>
        public static void ApplyUnified(ZatcaDetails details, string? apiEnvironment)
        {
            var effective = ResolveEffective(apiEnvironment, details.Environment);
            details.ApiEnvironment = effective;
            details.Environment = effective;
        }

        /// <summary>Returns true when fields were corrected in memory (caller should SaveChanges).</summary>
        public static bool TryAlign(ZatcaDetails details, out string effectiveEnvironment)
        {
            effectiveEnvironment = ResolveEffective(details.ApiEnvironment, details.Environment);

            var apiNormalized = string.IsNullOrWhiteSpace(details.ApiEnvironment)
                ? null
                : ZatcaApiConstants.NormalizeEnvironment(details.ApiEnvironment);
            var envNormalized = string.IsNullOrWhiteSpace(details.Environment)
                ? null
                : ZatcaApiConstants.NormalizeEnvironment(details.Environment);

            var needsApi = apiNormalized != effectiveEnvironment;
            var needsEnv = envNormalized != effectiveEnvironment;

            if (!needsApi && !needsEnv)
            {
                return false;
            }

            details.ApiEnvironment = effectiveEnvironment;
            details.Environment = effectiveEnvironment;
            return true;
        }

        public static async Task<ZatcaDetails?> LoadAlignedForHotelAsync(
            ApplicationDbContext db,
            int hotelId,
            CancellationToken cancellationToken = default)
        {
            var details = await db.ZatcaDetails
                .FirstOrDefaultAsync(z => z.HotelId == hotelId, cancellationToken);
            return await AlignAndSaveIfNeededAsync(db, details, cancellationToken);
        }

        /// <summary>Single-row tenant DB (current hotel context).</summary>
        public static async Task<ZatcaDetails?> LoadAlignedAsync(
            ApplicationDbContext db,
            CancellationToken cancellationToken = default)
        {
            var details = await db.ZatcaDetails.FirstOrDefaultAsync(cancellationToken);
            return await AlignAndSaveIfNeededAsync(db, details, cancellationToken);
        }

        private static async Task<ZatcaDetails?> AlignAndSaveIfNeededAsync(
            ApplicationDbContext db,
            ZatcaDetails? details,
            CancellationToken cancellationToken)
        {
            if (details == null)
            {
                return null;
            }

            if (TryAlign(details, out _))
            {
                details.UpdatedAt = KsaTime.Now;
                await db.SaveChangesAsync(cancellationToken);
            }

            return details;
        }
    }
}
