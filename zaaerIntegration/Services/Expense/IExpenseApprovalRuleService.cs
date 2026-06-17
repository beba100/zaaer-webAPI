using FinanceLedgerAPI.Models;

namespace zaaerIntegration.Services.Expense
{
    /// <summary>
    /// Service interface for managing expense approval rules
    /// خدمة لإدارة قواعد موافقة المصروفات
    /// </summary>
    public interface IExpenseApprovalRuleService
    {
        /// <summary>
        /// Get the applicable approval rule for a given role, status, amount, and category
        /// الحصول على القاعدة المناسبة للموافقة بناءً على الدور والحالة والمبلغ والفئة
        /// </summary>
        /// <param name="roleCode">Role code (e.g., 'verifier', 'supervisor', 'manager')</param>
        /// <param name="fromStatus">Current expense status (e.g., 'pending', 'awaiting-verifier')</param>
        /// <param name="amount">Expense amount</param>
        /// <param name="expenseCategoryId">Expense category ID (optional, null for all categories)</param>
        /// <returns>The applicable rule, or null if no rule matches</returns>
        Task<ExpenseApprovalRule?> GetApplicableRuleAsync(string roleCode, string fromStatus, decimal amount, int? expenseCategoryId = null);

        /// <summary>
        /// Get the next status for an expense based on approval rules
        /// الحصول على الحالة التالية للمصروف بناءً على قواعد الموافقة
        /// </summary>
        /// <param name="roleCode">Role code</param>
        /// <param name="fromStatus">Current expense status</param>
        /// <param name="amount">Expense amount</param>
        /// <param name="expenseCategoryId">Expense category ID (optional)</param>
        /// <returns>Next status string, or null if no rule matches</returns>
        Task<string?> GetNextStatusAsync(string roleCode, string fromStatus, decimal amount, int? expenseCategoryId = null);

        /// <summary>
        /// Get all active rules for a specific role and status
        /// الحصول على جميع القواعد النشطة لدور وحالة معينة
        /// </summary>
        /// <param name="roleCode">Role code</param>
        /// <param name="fromStatus">Current expense status</param>
        /// <returns>List of active rules</returns>
        Task<IEnumerable<ExpenseApprovalRule>> GetRulesAsync(string roleCode, string fromStatus);

        /// <summary>
        /// Validate if an amount meets the minimum requirement (>= 1 SAR)
        /// التحقق من أن المبلغ يلبي الحد الأدنى (>= 1 ريال)
        /// </summary>
        /// <param name="amount">Expense amount</param>
        /// <returns>True if amount is valid, false otherwise</returns>
        bool IsValidAmount(decimal amount);
    }
}

