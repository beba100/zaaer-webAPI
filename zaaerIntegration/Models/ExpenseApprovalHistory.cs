using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Expense Approval History - سجل موافقات المصروف
    /// لتتبع مسار طلب المصروف من الإنشاء حتى الموافقة النهائية أو الرفض
    /// </summary>
    [Table("expense_approval_history")]
    public class ExpenseApprovalHistory
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// معرف المصروف
        /// </summary>
        [Column("expense_id")]
        [Required]
        public long ExpenseId { get; set; }

        /// <summary>Legacy link — mirrors expenses.old_expense_id on tenant DBs.</summary>
        [Column("old_expense_id")]
        public int OldExpenseId { get; set; }

        /// <summary>
        /// نوع الإجراء: created, approved, rejected, awaiting-manager, awaiting-accountant, awaiting-admin
        /// </summary>
        [Column("action")]
        [Required]
        [MaxLength(50)]
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// معرف المستخدم الذي قام بالإجراء (من MasterUsers)
        /// </summary>
        [Column("action_by")]
        public int? ActionBy { get; set; }

        /// <summary>
        /// الاسم الكامل للمستخدم (للعرض السريع بدون join)
        /// </summary>
        [Column("action_by_full_name")]
        [MaxLength(200)]
        public string? ActionByFullName { get; set; }

        /// <summary>
        /// تاريخ ووقت الإجراء
        /// </summary>
        [Column("action_at")]
        [Required]
        public DateTime ActionAt { get; set; } = KsaTime.UtcNow;

        /// <summary>
        /// الحالة بعد هذا الإجراء: pending, accepted, rejected, awaiting-manager, awaiting-accountant, awaiting-admin
        /// </summary>
        [Column("status")]
        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// سبب الرفض (إذا كان الإجراء رفض)
        /// </summary>
        [Column("rejection_reason")]
        [MaxLength(500)]
        public string? RejectionReason { get; set; }

        /// <summary>
        /// ملاحظات إضافية
        /// </summary>
        [Column("comments")]
        [MaxLength(500)]
        public string? Comments { get; set; }

        /// <summary>
        /// التوصية (اختياري) - Recommendation
        /// </summary>
        [Column("recommendation")]
        [MaxLength(1000)]
        public string? Recommendation { get; set; }

        /// <summary>
        /// معرف المستخدم المستهدف للتوصية (NULL = للجميع)
        /// Recommendation target user ID (NULL = for everyone)
        /// </summary>
        [Column("recommendation_to_user_id")]
        public int? RecommendationToUserId { get; set; }

        /// <summary>
        /// قائمة معرفات المستخدمين الذين قرأوا التوصية (JSON array)
        /// List of user IDs who read the recommendation (JSON array)
        /// </summary>
        [Column("recommendation_read_by")]
        public string? RecommendationReadBy { get; set; }

        // Navigation property
        [ForeignKey("ExpenseId")]
        public Expense? Expense { get; set; }
    }
}

