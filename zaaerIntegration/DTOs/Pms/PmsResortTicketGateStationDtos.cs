#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.Pms
{
    public sealed class PmsResortTicketGateStationDto
    {
        public string StationCode { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string? NameEn { get; set; }
        public string TicketCategory { get; set; } = "other";
        public string GateUrl { get; set; } = string.Empty;
        public string GateHomeTileUrl { get; set; } = string.Empty;
        public string ManifestUrl { get; set; } = string.Empty;
        public string IconUrl192 { get; set; } = string.Empty;
        public string IconUrl512 { get; set; } = string.Empty;
        public string ThemeColor { get; set; } = "#0f172a";
    }

    public sealed class SaveRoleGateStationsDto
    {
        public List<string> StationCodes { get; set; } = new();
    }
}
