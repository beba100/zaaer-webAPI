using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    [Table("pms_users")]
    public class MasterRbacUser
    {
        [Key]
        [Column("user_id")]
        public int UserId { get; set; }

        [Column("zaaer_id")]
        public int? ZaaerId { get; set; }

        [Column("master_user_id")]
        public int? MasterUserId { get; set; }

        [Column("username")]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [Column("employee_number")]
        [MaxLength(50)]
        public string? EmployeeNumber { get; set; }

        [Column("status")]
        public bool Status { get; set; } = true;

        [Column("user_type")]
        [MaxLength(50)]
        public string UserType { get; set; } = "employee";

        [Column("first_name")]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Column("last_name")]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Column("first_name_en")]
        [MaxLength(100)]
        public string? FirstNameEn { get; set; }

        [Column("last_name_en")]
        [MaxLength(100)]
        public string? LastNameEn { get; set; }

        [Column("title")]
        [MaxLength(100)]
        public string? Title { get; set; }

        [Column("email")]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [Column("phone_number")]
        [MaxLength(30)]
        public string? PhoneNumber { get; set; }

        [Column("department")]
        [MaxLength(100)]
        public string? Department { get; set; }

        [Column("password_hash")]
        [MaxLength(500)]
        public string PasswordHash { get; set; } = string.Empty;

        [Column("change_password")]
        public bool ChangePassword { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("last_login")]
        public DateTime? LastLogin { get; set; }

        [Column("session_version")]
        public int SessionVersion { get; set; }

        [Column("is_locked")]
        public bool IsLocked { get; set; }

        [Column("locked_at")]
        public DateTime? LockedAt { get; set; }

        [Column("locked_reason")]
        [MaxLength(500)]
        public string? LockedReason { get; set; }

        public MasterUser? MasterUser { get; set; }
    }
}
