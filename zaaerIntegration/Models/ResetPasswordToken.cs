using System.ComponentModel.DataAnnotations;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Reset Password Token Model - لتخزين رموز إعادة تعيين كلمة المرور
    /// </summary>
    public class ResetPasswordToken
    {
        /// <summary>
        /// معرف الرمز
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// معرف المستخدم
        /// </summary>
        [Required]
        public int UserId { get; set; }

        /// <summary>
        /// الرمز المميز (Token) - يتم إنشاؤه بشكل عشوائي وآمن
        /// </summary>
        [Required]
        [StringLength(256)]
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// تاريخ انتهاء الصلاحية
        /// </summary>
        [Required]
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// هل تم استخدام الرمز
        /// </summary>
        public bool IsUsed { get; set; } = false;

        /// <summary>
        /// تاريخ الاستخدام (إذا تم استخدامه)
        /// </summary>
        public DateTime? UsedAt { get; set; }

        /// <summary>
        /// عنوان IP الذي طلب إعادة التعيين
        /// </summary>
        [StringLength(50)]
        public string? RequestIpAddress { get; set; }

        /// <summary>
        /// تاريخ الإنشاء
        /// </summary>
        public DateTime CreatedAt { get; set; } = KsaTime.UtcNow;

        /// <summary>
        /// Navigation Property - المستخدم
        /// </summary>
        public MasterUser? User { get; set; }
    }
}

