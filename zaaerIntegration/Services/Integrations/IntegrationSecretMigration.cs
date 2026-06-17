using FinanceLedgerAPI.Models;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Integrations
{
    /// <summary>
    /// Moves hotel integration secrets from legacy Data Protection storage to durable AES (MasterKey).
    /// </summary>
    internal static class IntegrationSecretMigration
    {
        internal static bool TryMigrateZatcaDevicePrivateKey(
            ZatcaDevice device,
            IIntegrationSecretProtector protector)
        {
            if (device.PrivateKeyEncrypted == null)
            {
                return false;
            }

            var rewrapped = protector.TryRewrapToDurable(device.PrivateKeyEncrypted);
            if (rewrapped == null)
            {
                return false;
            }

            device.PrivateKeyEncrypted = rewrapped;
            device.UpdatedAt = KsaTime.Now;
            return true;
        }
    }
}
