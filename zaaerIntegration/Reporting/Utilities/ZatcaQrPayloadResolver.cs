namespace zaaerIntegration.Reporting.Utilities;

/// <summary>Decodes <c>invoices.zatca_qr</c> (TLV as base64 per Zatca.EInvoice signing) for report rendering.</summary>
public static class ZatcaQrPayloadResolver
{
    public sealed class ZatcaQrPayload
    {
        public static ZatcaQrPayload Empty { get; } = new();

        public byte[]? TlvBytes { get; init; }
        public byte[]? ImageBytes { get; init; }

        public bool HasPayload =>
            TlvBytes is { Length: > 0 } || ImageBytes is { Length: > 0 };
    }

    public static ZatcaQrPayload Resolve(string? zatcaQr)
    {
        if (string.IsNullOrWhiteSpace(zatcaQr))
        {
            return ZatcaQrPayload.Empty;
        }

        var payload = zatcaQr.Trim();
        if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = payload.IndexOf(',');
            if (comma >= 0)
            {
                payload = payload[(comma + 1)..];
            }
        }

        payload = NormalizeBase64(payload);

        try
        {
            var bytes = Convert.FromBase64String(payload);
            if (bytes.Length == 0)
            {
                return ZatcaQrPayload.Empty;
            }

            if (IsRasterImage(bytes))
            {
                return new ZatcaQrPayload { ImageBytes = bytes };
            }

            if (!LooksLikeZatcaTlv(bytes))
            {
                return ZatcaQrPayload.Empty;
            }

            return new ZatcaQrPayload { TlvBytes = bytes };
        }
        catch
        {
            return ZatcaQrPayload.Empty;
        }
    }

    private static string NormalizeBase64(string value)
    {
        var s = value.Replace("\r", "", StringComparison.Ordinal)
            .Replace("\n", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal);

        s = s.Replace('-', '+').Replace('_', '/');

        var mod = s.Length % 4;
        if (mod == 2)
        {
            s += "==";
        }
        else if (mod == 3)
        {
            s += "=";
        }

        return s;
    }

    private static bool LooksLikeZatcaTlv(byte[] bytes)
    {
        if (bytes.Length < 3)
        {
            return false;
        }

        // ZATCA Phase-1 QR TLV starts with tag 1 (seller name), then length, then value.
        return bytes[0] is >= 1 and <= 9;
    }

    private static bool IsRasterImage(byte[] bytes) =>
        bytes.Length > 3
        && ((bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E)
            || (bytes[0] == 0xFF && bytes[1] == 0xD8));
}
