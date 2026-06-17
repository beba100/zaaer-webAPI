using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    [Table("pms_roles")]
    public class MasterRbacRole
    {
        [Key]
        [Column("role_id")]
        public int RoleId { get; set; }

        [Column("role_name")]
        [MaxLength(150)]
        public string RoleName { get; set; } = string.Empty;

        [Column("role_name_ar")]
        [MaxLength(150)]
        public string? RoleNameAr { get; set; }

        [Column("role_name_en")]
        [MaxLength(150)]
        public string? RoleNameEn { get; set; }

        [Column("role_description")]
        [MaxLength(500)]
        public string? RoleDescription { get; set; }

        [Column("role_code")]
        [MaxLength(100)]
        public string? RoleCode { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("updated_by")]
        public int? UpdatedBy { get; set; }

        [Column("zaaer_id")]
        public int? ZaaerId { get; set; }
    }
}
