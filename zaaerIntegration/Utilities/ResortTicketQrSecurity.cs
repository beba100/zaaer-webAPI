using System.Security.Cryptography;
using System.Text;

namespace zaaerIntegration.Utilities
{
    /// <summary>
    /// Signed resort ticket QR payloads (RSRT2) — prevents forging valid codes without the server secret.
    /// Legacy RSRT-{hotel}-{zaaer} remains valid for tickets already issued (must exist in DB).
    /// </summary>
    public sealed class ResortTicketQrSecurity
    {
        private const string SignedPrefix = "RSRT2.";
        private const string LegacyPrefix = "RSRT-";

        private readonly byte[] _keyBytes;

        public ResortTicketQrSecurity(IConfiguration configuration)
        {
            var secret = configuration["ResortTickets:QrSigningKey"]?.Trim();
            if (string.IsNullOrWhiteSpace(secret))
            {
                var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? Environments.Production;
                if (!Environments.Development.Equals(environment, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "ResortTickets:QrSigningKey is required in non-Development environments.");
                }

                secret = configuration["Jwt:SecretKey"]?.Trim()
                    ?? "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!";
            }

            if (secret.Length < 32)
            {
                throw new InvalidOperationException("ResortTickets:QrSigningKey must be at least 32 characters.");
            }

            _keyBytes = Encoding.UTF8.GetBytes(secret);
        }

        public string BuildSignedQrCode(int hotelId, int zaaerId, int ticketTypeId)
        {
            var signature = ComputeSignature(hotelId, zaaerId, ticketTypeId);
            return $"{SignedPrefix}{hotelId}.{zaaerId}.{signature}";
        }

        public bool IsSignedFormat(string qrCode) =>
            qrCode.Trim().StartsWith(SignedPrefix, StringComparison.OrdinalIgnoreCase);

        public bool IsLegacyFormat(string qrCode) =>
            qrCode.Trim().StartsWith(LegacyPrefix, StringComparison.OrdinalIgnoreCase);

        public bool IsKnownFormat(string qrCode)
        {
            var trimmed = qrCode?.Trim() ?? string.Empty;
            return IsSignedFormat(trimmed) || IsLegacyFormat(trimmed);
        }

        public bool VerifySignedForTicket(int hotelId, int zaaerId, int ticketTypeId, string qrCode)
        {
            if (!TryParseSigned(qrCode, out var parsedHotelId, out var parsedZaaerId, out var providedSig))
            {
                return false;
            }

            if (parsedHotelId != hotelId || parsedZaaerId != zaaerId)
            {
                return false;
            }

            return string.Equals(ComputeSignature(hotelId, zaaerId, ticketTypeId), providedSig, StringComparison.Ordinal);
        }

        public bool TryParseSigned(string qrCode, out int hotelId, out int zaaerId, out string signature)
        {
            hotelId = 0;
            zaaerId = 0;
            signature = string.Empty;
            var trimmed = qrCode?.Trim() ?? string.Empty;
            if (!trimmed.StartsWith(SignedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var body = trimmed[SignedPrefix.Length..];
            var parts = body.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 3)
            {
                return false;
            }

            if (!int.TryParse(parts[0], out hotelId) || !int.TryParse(parts[1], out zaaerId))
            {
                return false;
            }

            signature = parts[2];
            return !string.IsNullOrWhiteSpace(signature);
        }

        public bool TryParseLegacy(string qrCode, out int hotelId, out int zaaerId)
        {
            hotelId = 0;
            zaaerId = 0;
            var trimmed = qrCode?.Trim() ?? string.Empty;
            if (!trimmed.StartsWith(LegacyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var body = trimmed[LegacyPrefix.Length..];
            var parts = body.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
            {
                return false;
            }

            return int.TryParse(parts[0], out hotelId) && int.TryParse(parts[1], out zaaerId);
        }

        private string ComputeSignature(int hotelId, int zaaerId, int ticketTypeId)
        {
            var payload = $"{hotelId}|{zaaerId}|{ticketTypeId}|RSRT";
            using var hmac = new HMACSHA256(_keyBytes);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
        }

        public static string BuildTicketNumber(int zaaerId, string? hotelCode)
        {
            var suffix = NormalizeHotelSuffix(hotelCode);
            return $"TCK-{zaaerId:D6}-{suffix}";
        }

        public static string NormalizeHotelSuffix(string? hotelCode)
        {
            if (string.IsNullOrWhiteSpace(hotelCode))
            {
                return "RSRT";
            }

            var cleaned = new string(hotelCode.Trim().ToUpperInvariant()
                .Where(c => char.IsLetterOrDigit(c) || c == '-')
                .ToArray());
            if (cleaned.Length > 12)
            {
                cleaned = cleaned[..12];
            }

            return string.IsNullOrWhiteSpace(cleaned) ? "RSRT" : cleaned;
        }
    }
}
