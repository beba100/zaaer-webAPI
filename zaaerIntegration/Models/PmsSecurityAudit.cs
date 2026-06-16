using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    [Table("pms_security_audit")]
    public class PmsSecurityAudit
    {
        [Key]
        [Column("audit_id")]
        public long AuditId { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("actor_user_id")]
        public int? ActorUserId { get; set; }

        [Column("event_type")]
        [MaxLength(50)]
        public string EventType { get; set; } = string.Empty;

        [Column("session_id")]
        public long? SessionId { get; set; }

        [Column("ip_address")]
        [MaxLength(64)]
        public string? IpAddress { get; set; }

        [Column("details")]
        public string? Details { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;
    }
}
