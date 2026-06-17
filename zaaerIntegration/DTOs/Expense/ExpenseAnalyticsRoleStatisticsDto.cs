namespace zaaerIntegration.DTOs.Expense
{
    /// <summary>
    /// DTO لعرض إحصائيات الأدوار (عدد الطلبات المقبولة وفي الانتظار لكل دور)
    /// DTO for displaying role statistics (accepted and pending requests count per role)
    /// </summary>
    public class ExpenseAnalyticsRoleStatisticsDto
    {
        /// <summary>
        /// اسم الدور (Role Name)
        /// </summary>
        public string RoleName { get; set; } = string.Empty;

        /// <summary>
        /// اسم الدور بالعربية (Role Display Name in Arabic)
        /// </summary>
        public string RoleDisplayName { get; set; } = string.Empty;

        /// <summary>
        /// اسم الفرع/الفندق (Tenant/Hotel Name)
        /// </summary>
        public string? TenantName { get; set; }

        /// <summary>
        /// معرف المستخدم (User ID) - للمستخدم الذي لديه هذا الدور في هذا الفرع
        /// </summary>
        public int? UserId { get; set; }

        /// <summary>
        /// اسم المستخدم (User Full Name) - للمستخدم الذي لديه هذا الدور في هذا الفرع
        /// </summary>
        public string? UserName { get; set; }

        /// <summary>
        /// عدد الطلبات المقبولة (Accepted Requests Count)
        /// </summary>
        public int AcceptedCount { get; set; }

        /// <summary>
        /// عدد الطلبات في الانتظار (Pending Requests Count)
        /// </summary>
        public int PendingCount { get; set; }

        /// <summary>
        /// إجمالي عدد الطلبات (Total Requests Count)
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// المبلغ الإجمالي للطلبات المقبولة (Total Amount of Accepted Requests)
        /// </summary>
        public decimal AcceptedAmount { get; set; }

        /// <summary>
        /// المبلغ الإجمالي للطلبات في الانتظار (Total Amount of Pending Requests)
        /// </summary>
        public decimal PendingAmount { get; set; }
    }
}

