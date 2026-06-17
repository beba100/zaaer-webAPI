using zaaerIntegration.DTOs.Integrations;

namespace zaaerIntegration.Services.Integrations
{
    public interface IPmsBaladyReportService
    {
        Task<IReadOnlyList<BaladyReportRowDto>> GetReportAsync(
            BaladyReportQueryDto query,
            CancellationToken cancellationToken = default);
    }
}
