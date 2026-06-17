using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Hotels (tenants) a PMS user may access — selected at user creation like Zaaer branches.
    /// </summary>
    [Table("pms_user_hotels")]
    public class PmsUserHotel
    {
        [Key]
        [Column("user_hotel_id")]
        public int UserHotelId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        public MasterRbacUser? User { get; set; }
        public Tenant? Tenant { get; set; }
    }
}
