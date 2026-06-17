using System.ComponentModel.DataAnnotations;

namespace zaaerIntegration.DTOs.Expense
{
    /// <summary>
    /// DTO لتحديث expense
    /// لا يحتاج HotelId - سيُقرأ من X-Hotel-Code header
    /// </summary>
    public class UpdateExpenseDto
    {
        public DateTime? DateTime { get; set; }

        public DateTime? DueDate { get; set; }

        [MaxLength(500)]
        public string? Comment { get; set; }

        public int? ExpenseCategoryId { get; set; }

        [Range(0, 100)]
        public decimal? TaxRate { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? TaxAmount { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? BeforeTaxAmount { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? TotalAmount { get; set; }

        /// <summary>
        /// When true, validate ExpenseCategoryId against tenant expense_categories (PMS).
        /// </summary>
        public bool UseTenantCategories { get; set; }

        /// <summary>
        /// قائمة الغرف المرتبطة بهذه النفقة (للتحديث)
        /// List of rooms to update for this expense
        /// </summary>
        public List<CreateExpenseRoomDto>? ExpenseRooms { get; set; }

        /// <summary>
        /// حالة الموافقة الجديدة (يُستخدم عند تحديث مصروف مرفوض لإعادة إرساله للموافقة)
        /// New approval status (used when updating a rejected expense to resubmit for approval)
        /// </summary>
        [MaxLength(30)]
        public string? ApprovalStatus { get; set; }

        /// <summary>
        /// سبب الرفض (null لإعادة ضبطه عند إعادة الإرسال)
        /// Rejection reason (null to reset when resubmitting)
        /// </summary>
        [MaxLength(500)]
        public string? RejectionReason { get; set; }

        /// <summary>
        /// معرف المستخدم الذي وافق/رفض (null لإعادة ضبطه عند إعادة الإرسال)
        /// User ID who approved/rejected (null to reset when resubmitting)
        /// </summary>
        public int? ApprovedBy { get; set; }

        /// <summary>
        /// تاريخ ووقت الموافقة/الرفض (null لإعادة ضبطه عند إعادة الإرسال)
        /// Date and time of approval/rejection (null to reset when resubmitting)
        /// </summary>
        public DateTime? ApprovedAt { get; set; }

        /// <summary>
        /// مصدر السداد (Branch أو Management) - يُستخدم لتحديد الحالة الجديدة
        /// Payment source (Branch or Management) - used to determine new status
        /// </summary>
        [MaxLength(20)]
        public string? PaymentSource { get; set; }
    }
}

