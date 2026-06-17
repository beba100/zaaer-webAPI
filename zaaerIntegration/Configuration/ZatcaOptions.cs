using zaaerIntegration.Services.Integrations.Zatca;

namespace zaaerIntegration.Configuration
{
  public sealed class ZatcaOptions
  {
    public const string SectionName = "Zatca";

    /// <summary>
    /// When true, POST onboard on simulation without OTP returns a friendly defer message instead of calling ZATCA.
    /// </summary>
    public bool DeferSimulationOtpOnboarding { get; set; } = true;

    /// <summary>
    /// Fallback Fatoora <c>Accept-Language</c> when no UI culture header is present (background jobs).
    /// Interactive PMS uses <c>X-Ui-Culture</c> from the browser (ar → AR, en → EN).
    /// </summary>
    public string AcceptLanguage { get; set; } = ZatcaApiConstants.AcceptLanguageArabic;

    /// <summary>
    /// When true, writes unsigned/signed UBL XML under <c>logs/zatca-xml/</c> before gateway submit (temporary diagnostics).
    /// </summary>
    public bool LogInvoiceXmlBeforeSubmit { get; set; } = true;
  }
}
