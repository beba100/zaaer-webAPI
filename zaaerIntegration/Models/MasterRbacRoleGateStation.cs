using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    [Table("pms_role_gate_stations")]
    public class MasterRbacRoleGateStation
    {
        [Key]
        [Column("role_gate_station_id")]
        public int RoleGateStationId { get; set; }

        [Column("role_id")]
        public int RoleId { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("station_code")]
        public string StationCode { get; set; } = string.Empty;

        [Column("sort_order")]
        public int SortOrder { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;
    }
}
