using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace zaaerIntegration.Services.Integrations.Zatca
{
  /// <summary>
  /// Writes UBL XML snapshots to <c>logs/zatca-xml/</c> for manual ZATCA review (temporary diagnostics).
  /// </summary>
  internal static class ZatcaUblXmlDiagnostics
  {
    public static void TryWrite(
      IHostEnvironment env,
      ILogger logger,
      string documentNo,
      ZatcaProfileResolution profile,
      string unsignedXml,
      string signedXml)
    {
      try
      {
        var dir = Path.Combine(env.ContentRootPath, "logs", "zatca-xml");
        Directory.CreateDirectory(dir);

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var safeDoc = SanitizeFileName(documentNo);
        var baseName = $"{safeDoc}-{stamp}-{profile.Profile}";

        var unsignedPath = Path.Combine(dir, $"{baseName}-unsigned.xml");
        var signedPath = Path.Combine(dir, $"{baseName}-signed.xml");

        File.WriteAllText(unsignedPath, unsignedXml);
        File.WriteAllText(signedPath, signedXml);

        logger.LogInformation(
          "[ZATCA UBL] Diagnostic XML for {DocumentNo}: profile={Profile} mode={Mode} → {UnsignedPath}",
          documentNo,
          profile.Profile,
          profile.SubmissionMode,
          unsignedPath);
      }
      catch (Exception ex)
      {
        logger.LogWarning(ex, "[ZATCA UBL] Could not write diagnostic XML for {DocumentNo}", documentNo);
      }
    }

    private static string SanitizeFileName(string value)
    {
      var invalid = Path.GetInvalidFileNameChars();
      var chars = value.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
      var name = new string(chars).Trim();
      return string.IsNullOrWhiteSpace(name) ? "document" : name;
    }
  }
}
