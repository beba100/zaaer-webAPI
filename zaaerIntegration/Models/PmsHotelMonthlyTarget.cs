using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    [Table("pms_hotel_monthly_targets")]
    public class PmsHotelMonthlyTarget
    {
        [Key]
        [Column("hotel_monthly_target_id")]
        public int HotelMonthlyTargetId { get; set; }

        [Column("hotel_zaaer_id")]
        public int HotelZaaerId { get; set; }

        [MaxLength(200)]
        [Column("branch_name")]
        public string? BranchName { get; set; }

        [Column("month_year", TypeName = "date")]
        public DateTime MonthYear { get; set; }

        [Column("target_amount", TypeName = "decimal(18,2)")]
        public decimal TargetAmount { get; set; }

        [Column("commission_before_85", TypeName = "decimal(9,4)")]
        public decimal CommissionBefore85 { get; set; }

        [Column("commission_at_85", TypeName = "decimal(9,4)")]
        public decimal CommissionAt85 { get; set; }

        [Column("commission_86_to_100", TypeName = "decimal(9,4)")]
        public decimal Commission86To100 { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
