using zaaerIntegration.Data;
using zaaerIntegration.Services.Integrations.Zatca;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Services.Integrations
{
  public interface IPmsZatcaComplianceService
  {
    Task<ZatcaComplianceRunResult> RunAllSixForCurrentHotelAsync(
      string? environment = null,
      CancellationToken cancellationToken = default);
  }

  public sealed class PmsZatcaComplianceService : PmsHotelScopeService, IPmsZatcaComplianceService
  {
    private readonly IZatcaComplianceService _compliance;

    public PmsZatcaComplianceService(
      ApplicationDbContext context,
      ITenantService tenantService,
      IZatcaComplianceService compliance)
      : base(context, tenantService)
    {
      _compliance = compliance;
    }

    public async Task<ZatcaComplianceRunResult> RunAllSixForCurrentHotelAsync(
      string? environment = null,
      CancellationToken cancellationToken = default)
    {
      var hotelId = await GetCurrentHotelIdAsync(cancellationToken);
      return await _compliance.RunAllSixAsync(hotelId, environment, cancellationToken);
    }
  }
}
