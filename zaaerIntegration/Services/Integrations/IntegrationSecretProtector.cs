using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using zaaerIntegration.Configuration;

namespace zaaerIntegration.Services.Integrations
{
    public interface IIntegrationSecretProtector
    {
        /// <summary>True when <see cref="IntegrationSecretsOptions.MasterKey"/> is configured on this host.</summary>
        bool IsMasterKeyConfigured { get; }

        byte[] Protect(string plainText);

        string? Unprotect(byte[]? protectedBytes);

        /// <summary>True when stored bytes can be decrypted with the current host configuration.</summary>
        bool CanUnprotect(byte[]? protectedBytes);

        /// <summary>True when bytes use durable AES (<c>ZIS1</c>) rather than Data Protection.</summary>
        bool IsDurableFormat(byte[]? protectedBytes);

        /// <summary>Re-encrypt legacy secrets with MasterKey when possible; returns new bytes or null if unchanged.</summary>
        byte[]? TryRewrapToDurable(byte[]? protectedBytes);
    }

    public sealed class IntegrationSecretProtector : IIntegrationSecretProtector
    {
        public const string DecryptFailedUserMessage =
            "Could not decrypt device private key. Configure IntegrationSecrets:MasterKey on the server, " +
            "then re-register the EGS device (OTP) for this hotel and environment.";

        public const string MasterKeyRequiredMessage =
            "IntegrationSecrets:MasterKey must be configured on the server before ZATCA device onboarding. " +
            "Set it once for the whole platform; each hotel keeps its own device keys in its database.";

        private static readonly byte[] AesPrefix = "ZIS1"u8.ToArray();
        private const int AesNonceSize = 12;
        private const int AesTagSize = 16;

        private readonly IDataProtector _protector;
        private readonly byte[]? _masterKey;
        private readonly ILogger<IntegrationSecretProtector> _logger;

        public bool IsMasterKeyConfigured => _masterKey != null;

        public IntegrationSecretProtector(
            IDataProtectionProvider provider,
            IOptions<IntegrationSecretsOptions> options,
            ILogger<IntegrationSecretProtector> logger)
        {
            _protector = provider.CreateProtector("zaaerIntegration.Integrations.Secrets.v1");
            _logger = logger;
            _masterKey = TryParseMasterKey(options.Value.MasterKey);
        }

        public byte[] Protect(string plainText)
        {
            var plain = Encoding.UTF8.GetBytes(plainText);
            if (_masterKey != null)
            {
                return ProtectAes(plain);
            }

            return _protector.Protect(plain);
        }

        public string? Unprotect(byte[]? protectedBytes) =>
            TryUnprotect(protectedBytes, out var plain) ? plain : null;

        public bool CanUnprotect(byte[]? protectedBytes) =>
            TryUnprotect(protectedBytes, out _);

        public bool IsDurableFormat(byte[]? protectedBytes) =>
            protectedBytes != null && protectedBytes.Length > 0 && StartsWith(protectedBytes, AesPrefix);

        public byte[]? TryRewrapToDurable(byte[]? protectedBytes)
        {
            if (protectedBytes == null || protectedBytes.Length == 0 || _masterKey == null)
            {
                return null;
            }

            if (IsDurableFormat(protectedBytes))
            {
                return null;
            }

            if (!TryUnprotect(protectedBytes, out var plainText) || string.IsNullOrWhiteSpace(plainText))
            {
                return null;
            }

            return Protect(plainText);
        }

        private bool TryUnprotect(byte[]? protectedBytes, out string? plainText)
        {
            plainText = null;
            if (protectedBytes == null || protectedBytes.Length == 0)
            {
                return false;
            }

            if (StartsWith(protectedBytes, AesPrefix))
            {
                plainText = UnprotectAes(protectedBytes);
                return !string.IsNullOrWhiteSpace(plainText);
            }

            var asText = TryUtf8(protectedBytes);
            if (IsPrivateKeyPem(asText))
            {
                _logger.LogWarning("Integration secret loaded from legacy plain PEM storage.");
                plainText = asText;
                return true;
            }

            try
            {
                plainText = Encoding.UTF8.GetString(_protector.Unprotect(protectedBytes));
                return !string.IsNullOrWhiteSpace(plainText);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Integration secret Data Protection decrypt failed. Restore App_Data/DataProtection-Keys or re-onboard.");
                return false;
            }
        }

        private byte[] ProtectAes(byte[] plain)
        {
            var nonce = new byte[AesNonceSize];
            RandomNumberGenerator.Fill(nonce);
            var tag = new byte[AesTagSize];
            var cipher = new byte[plain.Length];
            using var aes = new AesGcm(_masterKey!, AesTagSize);
            aes.Encrypt(nonce, plain, cipher, tag);

            var output = new byte[AesPrefix.Length + nonce.Length + tag.Length + cipher.Length];
            Buffer.BlockCopy(AesPrefix, 0, output, 0, AesPrefix.Length);
            Buffer.BlockCopy(nonce, 0, output, AesPrefix.Length, nonce.Length);
            Buffer.BlockCopy(tag, 0, output, AesPrefix.Length + nonce.Length, tag.Length);
            Buffer.BlockCopy(cipher, 0, output, AesPrefix.Length + nonce.Length + tag.Length, cipher.Length);
            return output;
        }

        private string? UnprotectAes(byte[] protectedBytes)
        {
            if (_masterKey == null)
            {
                _logger.LogError(
                    "Integration secret uses durable AES encryption but IntegrationSecrets:MasterKey is not configured.");
                return null;
            }

            var minLength = AesPrefix.Length + AesNonceSize + AesTagSize;
            if (protectedBytes.Length <= minLength)
            {
                return null;
            }

            try
            {
                var nonce = new byte[AesNonceSize];
                var tag = new byte[AesTagSize];
                var cipherLength = protectedBytes.Length - minLength;
                var cipher = new byte[cipherLength];
                var plain = new byte[cipherLength];

                Buffer.BlockCopy(protectedBytes, AesPrefix.Length, nonce, 0, AesNonceSize);
                Buffer.BlockCopy(protectedBytes, AesPrefix.Length + AesNonceSize, tag, 0, AesTagSize);
                Buffer.BlockCopy(protectedBytes, minLength, cipher, 0, cipherLength);

                using var aes = new AesGcm(_masterKey, AesTagSize);
                aes.Decrypt(nonce, cipher, tag, plain);
                return Encoding.UTF8.GetString(plain);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Integration secret AES decrypt failed.");
                return null;
            }
        }

        private static byte[]? TryParseMasterKey(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            try
            {
                var key = Convert.FromBase64String(raw.Trim());
                return key.Length == 32 ? key : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool StartsWith(byte[] data, byte[] prefix)
        {
            if (data.Length < prefix.Length)
            {
                return false;
            }

            for (var i = 0; i < prefix.Length; i++)
            {
                if (data[i] != prefix[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static string? TryUtf8(byte[] data)
        {
            try
            {
                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsPrivateKeyPem(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.Contains("BEGIN PRIVATE KEY", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("BEGIN RSA PRIVATE KEY", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("BEGIN EC PRIVATE KEY", StringComparison.OrdinalIgnoreCase);
        }
    }
}
