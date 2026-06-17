using Zatca.EInvoice.Api;
using Zatca.EInvoice.Certificates;

namespace zaaerIntegration.Services.Integrations.Zatca
{
  public static class ZatcaEInvoiceEnvironmentMapper
  {
    public static ZatcaEnvironment ToApiEnvironment(string? environment) =>
      ZatcaApiConstants.NormalizeEnvironment(environment) switch
      {
        "simulation" => ZatcaEnvironment.Simulation,
        "production" => ZatcaEnvironment.Production,
        _ => ZatcaEnvironment.Sandbox
      };

    public static ZatcaEnvironmentMode ToCertificateEnvironmentMode(string? environment) =>
      ZatcaApiConstants.NormalizeEnvironment(environment) switch
      {
        "simulation" => ZatcaEnvironmentMode.Simulation,
        "production" => ZatcaEnvironmentMode.Production,
        _ => ZatcaEnvironmentMode.Sandbox
      };
  }
}
