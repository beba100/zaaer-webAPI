namespace zaaerIntegration.DTOs.Expense
{
    /// <summary>
    /// DTO لعرض توزيع حالات المصروفات
    /// DTO for displaying expense status distribution
    /// </summary>
    public class ExpenseAnalyticsStatusDistributionDto
    {
        /// <summary>
        /// حالة المصروف
        /// Expense status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// اسم الحالة بالعربية
        /// Status name in Arabic
        /// </summary>
        public string StatusName { get; set; } = string.Empty;

        /// <summary>
        /// عدد المصروفات
        /// Number of expenses
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// المبلغ الإجمالي
        /// Total amount
        /// </summary>
        public decimal Amount { get; set; }
    }
}

