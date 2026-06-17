namespace zaaerIntegration.DTOs.Expense
{
    /// <summary>
    /// DTO لعرض مؤشرات الأداء الرئيسية (KPIs) للتحليلات
    /// DTO for displaying Key Performance Indicators (KPIs) for analytics
    /// </summary>
    public class ExpenseAnalyticsKpiDto
    {
        /// <summary>
        /// إجمالي المصروفات
        /// Total expenses amount
        /// </summary>
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// عدد المصروفات
        /// Total number of expenses
        /// </summary>
        public int ExpenseCount { get; set; }

        /// <summary>
        /// متوسط المصروف
        /// Average expense amount
        /// </summary>
        public decimal AverageAmount { get; set; }

        /// <summary>
        /// عدد الفنادق
        /// Number of hotels
        /// </summary>
        public int HotelCount { get; set; }
    }
}

