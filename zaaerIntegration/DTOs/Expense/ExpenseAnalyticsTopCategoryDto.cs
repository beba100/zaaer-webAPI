namespace zaaerIntegration.DTOs.Expense
{
    /// <summary>
    /// DTO لعرض أعلى أنواع المصروفات
    /// DTO for displaying top expense categories
    /// </summary>
    public class ExpenseAnalyticsTopCategoryDto
    {
        /// <summary>
        /// اسم فئة المصروف
        /// Expense category name
        /// </summary>
        public string CategoryName { get; set; } = string.Empty;

        /// <summary>
        /// المبلغ الإجمالي
        /// Total amount
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// عدد المصروفات
        /// Number of expenses
        /// </summary>
        public int Count { get; set; }
    }
}

