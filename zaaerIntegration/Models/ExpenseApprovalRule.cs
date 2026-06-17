using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Expense Approval Rule Model - قواعد موافقة المصروفات
    /// Stores business rules for expense approval workflows
    /// </summary>
    [Table("ExpenseApprovalRules")]
    public class ExpenseApprovalRule
    {
        /// <summary>
        /// معرف القاعدة (Rule ID)
        /// </summary>
        [Key]
        [Column("RuleId")]
        public int RuleId { get; set; }

        /// <summary>
        /// رمز الدور (Role Code): 'verifier', 'supervisor', 'manager', 'accountant', 'admin', 'officer'
        /// </summary>
        [Required]
        [MaxLength(50)]
        [Column("RoleCode")]
        public string RoleCode { get; set; } = string.Empty;

        /// <summary>
        /// الحالة الحالية للمصروف (From Status): 'pending', 'awaiting-verifier', 'awaiting-manager', etc.
        /// </summary>
        [Required]
        [MaxLength(50)]
        [Column("FromStatus")]
        public string FromStatus { get; set; } = string.Empty;

        /// <summary>
        /// الحد الأدنى للمبلغ (Minimum Amount) - NULL = لا يوجد حد أدنى
        /// </summary>
        [Column("MinAmount", TypeName = "decimal(12,2)")]
        public decimal? MinAmount { get; set; }

        /// <summary>
        /// الحد الأقصى للمبلغ (Maximum Amount) - NULL = لا يوجد حد أقصى
        /// </summary>
        [Column("MaxAmount", TypeName = "decimal(12,2)")]
        public decimal? MaxAmount { get; set; }

        /// <summary>
        /// عامل المقارنة للمبلغ (Amount Comparison Operator): '<=', '<', '>=', '>', 'between'
        /// </summary>
        [MaxLength(10)]
        [Column("AmountComparisonOperator")]
        public string? AmountComparisonOperator { get; set; }

        /// <summary>
        /// معرف فئة المصروف (Expense Category ID) - NULL = ينطبق على جميع الفئات
        /// </summary>
        [Column("ExpenseCategoryId")]
        public int? ExpenseCategoryId { get; set; }

        /// <summary>
        /// شرط فئة المصروف (Category Condition): 'equals', 'not_equals'
        /// </summary>
        [MaxLength(20)]
        [Column("ExpenseCategoryCondition")]
        public string? ExpenseCategoryCondition { get; set; }

        /// <summary>
        /// الحالة التالية (Next Status): 'accepted', 'awaiting-manager', 'awaiting-accountant', etc.
        /// </summary>
        [Required]
        [MaxLength(50)]
        [Column("NextStatus")]
        public string NextStatus { get; set; } = string.Empty;

        /// <summary>
        /// الأولوية (Priority) - رقم أقل = أولوية أعلى (يتم تقييمه أولاً)
        /// </summary>
        [Required]
        [Column("Priority")]
        public int Priority { get; set; } = 100;

        /// <summary>
        /// هل القاعدة نشطة (Is Active)
        /// </summary>
        [Required]
        [Column("IsActive")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// الوصف (Description)
        /// </summary>
        [MaxLength(500)]
        [Column("Description")]
        public string? Description { get; set; }

        /// <summary>
        /// تاريخ الإنشاء (Created At)
        /// </summary>
        [Required]
        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = KsaTime.UtcNow;

        /// <summary>
        /// معرف المستخدم الذي أنشأ القاعدة (Created By)
        /// </summary>
        [Column("CreatedBy")]
        public int? CreatedBy { get; set; }

        /// <summary>
        /// تاريخ آخر تحديث (Updated At)
        /// </summary>
        [Column("UpdatedAt")]
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// معرف المستخدم الذي قام بآخر تحديث (Updated By)
        /// </summary>
        [Column("UpdatedBy")]
        public int? UpdatedBy { get; set; }
    }
}

