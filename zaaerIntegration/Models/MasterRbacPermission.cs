using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    [Table("pms_permissions")]
    public class MasterRbacPermission
    {
        [Key]
        [Column("permission_id")]
        public int PermissionId { get; set; }

        [Column("permission_name")]
        [MaxLength(150)]
        public string PermissionName { get; set; } = string.Empty;

        [Column("permission_name_ar")]
        [MaxLength(200)]
        public string? PermissionNameAr { get; set; }

        [Column("permission_name_en")]
        [MaxLength(200)]
        public string? PermissionNameEn { get; set; }

        [Column("permission_code")]
        [MaxLength(150)]
        public string PermissionCode { get; set; } = string.Empty;

        [Column("module_name")]
        [MaxLength(80)]
        public string ModuleName { get; set; } = string.Empty;

        [Column("submodule_name")]
        [MaxLength(80)]
        public string? SubmoduleName { get; set; }

        [Column("action_name")]
        [MaxLength(80)]
        public string ActionName { get; set; } = string.Empty;

        [Column("sort_order")]
        public int SortOrder { get; set; }

        [Column("description")]
        [MaxLength(500)]
        public string? Description { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("zaaer_id")]
        public int? ZaaerId { get; set; }
    }
}
