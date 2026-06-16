namespace zaaerIntegration.Services.Auth
{
    public class JwtTokenDescriptor
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public int TenantId { get; set; }
        public string? TenantCode { get; set; }
        public string AuthMode { get; set; } = "CentralManaged";
        public IEnumerable<string> Roles { get; set; } = Array.Empty<string>();
        public IEnumerable<string> Permissions { get; set; } = Array.Empty<string>();
        public IEnumerable<int> AllowedHotelIds { get; set; } = Array.Empty<int>();
        public IEnumerable<int> AllowedGroupIds { get; set; } = Array.Empty<int>();
        public int SessionVersion { get; set; }
        public long? SessionId { get; set; }
    }
}
