using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    [Table("pms_user_sessions")]
    public class PmsUserSession
    {
        [Key]
        [Column("session_id")]
        public long SessionId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("refresh_token_hash")]
        [MaxLength(128)]
        public string RefreshTokenHash { get; set; } = string.Empty;

        [Column("device_id")]
        [MaxLength(100)]
        public string? DeviceId { get; set; }

        [Column("device_name")]
        [MaxLength(200)]
        public string? DeviceName { get; set; }

        [Column("ip_address")]
        [MaxLength(64)]
        public string? IpAddress { get; set; }

        [Column("user_agent")]
        [MaxLength(500)]
        public string? UserAgent { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [Column("last_activity_at")]
        public DateTime LastActivityAt { get; set; } = KsaTime.Now;

        [Column("revoked_at")]
        public DateTime? RevokedAt { get; set; }

        [Column("revoked_by")]
        public int? RevokedBy { get; set; }

        [Column("revoke_reason")]
        [MaxLength(200)]
        public string? RevokeReason { get; set; }

        public MasterRbacUser? User { get; set; }
    }
}
