namespace zaaerIntegration.DTOs.Expense
{
    /// <summary>
    /// DTO لعرض أعلى الفنادق صرفت مصروفات
    /// DTO for displaying top hotels by expenses
    /// </summary>
    public class ExpenseAnalyticsTopHotelDto
    {
        /// <summary>
        /// اسم الفندق
        /// Hotel name
        /// </summary>
        public string HotelName { get; set; } = string.Empty;

        /// <summary>
        /// كود الفندق
        /// Hotel code
        /// </summary>
        public string? HotelCode { get; set; }

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

        /// <summary>
        /// النسبة المئوية من الإجمالي
        /// Percentage of total
        /// </summary>
        public decimal Percentage { get; set; }
    }
}

