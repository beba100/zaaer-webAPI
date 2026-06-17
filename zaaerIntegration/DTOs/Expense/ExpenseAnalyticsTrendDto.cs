namespace zaaerIntegration.DTOs.Expense
{
    /// <summary>
    /// DTO لعرض اتجاهات المصروفات
    /// DTO for displaying expense trends
    /// </summary>
    public class ExpenseAnalyticsTrendDto
    {
        /// <summary>
        /// التاريخ
        /// Date
        /// </summary>
        public DateTime Date { get; set; }

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

