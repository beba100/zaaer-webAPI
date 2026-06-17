using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace zaaerIntegration.Services.Integrations.Zatca
{
  /// <summary>
  /// Builds an <see cref="X509Certificate2"/> with private key for ZATCA invoice signing.
  /// ZATCA stores the compliance token (base64) and CSR private key separately in <c>zatca_devices</c>.
  /// </summary>
  internal static class ZatcaSigningCertificateFactory
  {
    public static X509Certificate2 CreateSigningCertificate(string rawCertificate, string privateKeyPem)
    {
      if (string.IsNullOrWhiteSpace(rawCertificate))
      {
        throw new ArgumentException("Compliance certificate token is missing.", nameof(rawCertificate));
      }

      if (string.IsNullOrWhiteSpace(privateKeyPem))
      {
        throw new ArgumentException("Device private key is missing.", nameof(privateKeyPem));
      }

      var certPem = ToCertificatePem(rawCertificate);
      var keyPem = NormalizePrivateKeyPem(privateKeyPem);

      var cert = X509Certificate2.CreateFromPem(certPem, keyPem);
      if (!cert.HasPrivateKey)
      {
        cert.Dispose();
        throw new InvalidOperationException(
          "Signing certificate was loaded without a private key. Re-onboard the device or verify the stored private key PEM.");
      }

      // Use the PEM-loaded cert directly. Re-importing via PKCS#12 + EphemeralKeySet fails on Windows/IIS
      // with CryptographicException: "The system cannot find the file specified."
      return cert;
    }

    private static string ToCertificatePem(string raw)
    {
      var trimmed = raw.Trim();
      if (trimmed.Contains("BEGIN CERTIFICATE", StringComparison.OrdinalIgnoreCase))
      {
        return trimmed;
      }

      var base64 = trimmed
        .Replace("-----BEGIN CERTIFICATE-----", "", StringComparison.OrdinalIgnoreCase)
        .Replace("-----END CERTIFICATE-----", "", StringComparison.OrdinalIgnoreCase)
        .Replace("\r", "")
        .Replace("\n", "")
        .Trim();

      var sb = new StringBuilder();
      sb.AppendLine("-----BEGIN CERTIFICATE-----");
      for (var i = 0; i < base64.Length; i += 64)
      {
        sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
      }

      sb.AppendLine("-----END CERTIFICATE-----");
      return sb.ToString();
    }

    private static string NormalizePrivateKeyPem(string privateKeyPem)
    {
      var trimmed = privateKeyPem.Trim();
      if (trimmed.Contains("BEGIN", StringComparison.OrdinalIgnoreCase))
      {
        return trimmed;
      }

      var base64 = trimmed.Replace("\r", "").Replace("\n", "").Trim();
      var sb = new StringBuilder();
      sb.AppendLine("-----BEGIN PRIVATE KEY-----");
      for (var i = 0; i < base64.Length; i += 64)
      {
        sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
      }

      sb.AppendLine("-----END PRIVATE KEY-----");
      return sb.ToString();
    }
  }
}
