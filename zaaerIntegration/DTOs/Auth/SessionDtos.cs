namespace zaaerIntegration.DTOs.Auth
{
    public class RefreshTokenRequestDto
    {
        public string RefreshToken { get; set; } = string.Empty;
        public string? DeviceId { get; set; }
        public string? HotelCode { get; set; }
        public int? TenantId { get; set; }
    }

    public class UserSessionDto
    {
        public long SessionId { get; set; }
        public string? DeviceId { get; set; }
        public string? DeviceName { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivityAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public bool IsActive { get; set; }
        public string Status { get; set; } = "Active";
        public bool IsCurrent { get; set; }
    }
}
