namespace zaaerIntegration.DTOs.Expense
{
    /// <summary>
    /// DTO لعرض تفاصيل مصروفات الفنادق في جدول
    /// DTO for displaying hotel expenses details in table
    /// </summary>
    public class ExpenseAnalyticsHotelTableDto
    {
        /// <summary>
        /// الترتيب
        /// Rank
        /// </summary>
        public int Rank { get; set; }

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
        /// عدد المصروفات
        /// Number of expenses
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// المبلغ الإجمالي
        /// Total amount
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// متوسط المصروف
        /// Average expense amount
        /// </summary>
        public decimal Average { get; set; }

        /// <summary>
        /// إجمالي مبلغ الضريبة
        /// Total tax amount
        /// </summary>
        public decimal TotalTaxAmount { get; set; }
    }
}

