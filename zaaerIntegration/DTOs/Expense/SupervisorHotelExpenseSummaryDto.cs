using System;

namespace zaaerIntegration.DTOs.Expense
{
    /// <summary>
    /// DTO لملخص مصروفات المشرف حسب الفندق
    /// </summary>
    public class SupervisorHotelExpenseSummaryDto
    {
        public int HotelId { get; set; }
        public string? HotelCode { get; set; }
        public string HotelName { get; set; } = string.Empty;

        /// <summary>
        /// إجمالي عدد سندات المصروف
        /// </summary>
        public int ExpenseCount { get; set; }

        /// <summary>
        /// إجمالي المبلغ (شامل الضريبة)
        /// </summary>
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// إجمالي مبلغ الضريبة
        /// </summary>
        public decimal TotalTaxAmount { get; set; }

        /// <summary>
        /// تاريخ البداية المستخدم في التقرير
        /// </summary>
        public DateTime FromDate { get; set; }

        /// <summary>
        /// تاريخ النهاية المستخدم في التقرير
        /// </summary>
        public DateTime ToDate { get; set; }
    }
}


