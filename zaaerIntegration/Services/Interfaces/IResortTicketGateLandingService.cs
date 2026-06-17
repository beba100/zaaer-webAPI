using zaaerIntegration.DTOs.Pms;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IResortTicketGateLandingService
    {
        Task<IReadOnlyList<PmsResortTicketGateStationDto>> GetStationCatalogAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PmsResortTicketGateStationDto>> GetUserGateStationsAsync(
            int userId,
            int tenantId,
            IReadOnlyCollection<string> permissions,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> GetRoleGateStationCodesAsync(int roleId, CancellationToken cancellationToken = default);

        Task SaveRoleGateStationCodesAsync(int roleId, IReadOnlyList<string> stationCodes, CancellationToken cancellationToken = default);

        PmsResortTicketGateStationDto? TryBuildStationDto(string stationCode, IReadOnlyDictionary<string, GateStationMeta> catalog);

        byte[] RenderStationIcon(string stationCode, int size);

        string BuildManifestJson(string stationCode, IReadOnlyDictionary<string, GateStationMeta> catalog);

        Task<IReadOnlyDictionary<string, GateStationMeta>> LoadCatalogMapAsync(
            int? tenantId,
            CancellationToken cancellationToken = default);

        string? ResolvePreferredLandingUrl(IReadOnlyList<PmsResortTicketGateStationDto> stations);
    }

    public sealed class GateStationMeta
    {
        public string StationCode { get; init; } = string.Empty;
        public string NameAr { get; init; } = string.Empty;
        public string? NameEn { get; init; }
        public string TicketCategory { get; init; } = "other";
    }
}
