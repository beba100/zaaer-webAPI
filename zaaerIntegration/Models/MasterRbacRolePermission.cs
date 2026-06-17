using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Central mapping between a PMS role and permission.
    /// </summary>
    [Table("pms_role_permissions")]
    public class MasterRbacRolePermission
    {
        [Key]
        [Column("role_permission_id")]
        public int RolePermissionId { get; set; }

        [Column("role_id")]
        public int RoleId { get; set; }

        [Column("permission_id")]
        public int PermissionId { get; set; }

        [Column("granted")]
        public bool Granted { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("zaaer_id")]
        public int? ZaaerId { get; set; }

        public MasterRbacRole? Role { get; set; }
        public MasterRbacPermission? Permission { get; set; }
    }
}
