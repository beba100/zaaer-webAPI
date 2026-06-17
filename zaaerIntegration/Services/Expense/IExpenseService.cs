using zaaerIntegration.DTOs.Expense;

namespace zaaerIntegration.Services.Expense
{
    /// <summary>
    /// Service interface لإدارة النفقات (Expenses)
    /// يستخدم X-Hotel-Code header للحصول على HotelId
    /// </summary>
    public interface IExpenseService
    {
        Task<IEnumerable<ExpenseResponseDto>> GetAllAsync();
        Task<ExpenseResponseDto?> GetByIdAsync(long id);
        Task<ExpenseResponseDto> CreateAsync(CreateExpenseDto dto);
        Task<ExpenseResponseDto?> UpdateAsync(long id, UpdateExpenseDto dto);
        Task<bool> DeleteAsync(long id);

        // Expense Rooms Operations
        Task<IEnumerable<ExpenseRoomResponseDto>> GetExpenseRoomsAsync(long expenseId);
        Task<ExpenseRoomResponseDto> AddExpenseRoomAsync(long expenseId, CreateExpenseRoomDto dto);
        Task<ExpenseRoomResponseDto?> UpdateExpenseRoomAsync(int expenseRoomId, UpdateExpenseRoomDto dto);
        Task<bool> DeleteExpenseRoomAsync(int expenseRoomId);

        // Approval Operations
        Task<ExpenseResponseDto?> ApproveExpenseAsync(long id, string status, int approvedBy, string? rejectionReason = null, string? recommendation = null, int? recommendationToUserId = null);

        // Approval History Operations
        Task<IEnumerable<ExpenseApprovalHistoryDto>> GetApprovalHistoryAsync(long expenseId);

        /// <summary>
        /// تقرير ملخص المصروفات حسب الفندق للمشرف (ملخص فقط، ليس تفاصيل السندات)
        /// </summary>
        Task<IEnumerable<ExpenseAnalyticsHotelTableDto>> GetSupervisorHotelSummaryAsync(
            DateTime fromDate,
            DateTime toDate,
            int? expenseCategoryId = null,
            string? approvalStatus = null);

        /// <summary>
        /// الحصول على تفاصيل المصروفات لفندق محدد في سياق المشرف
        /// Get expense details for a specific hotel in supervisor context
        /// </summary>
        /// <param name="hotelCode">كود الفندق</param>
        /// <param name="fromDate">تاريخ البداية</param>
        /// <param name="toDate">تاريخ النهاية</param>
        /// <param name="expenseCategoryId">معرف فئة المصروف (اختياري)</param>
        /// <param name="approvalStatus">حالة الموافقة (اختياري)</param>
        /// <returns>قائمة تفاصيل المصروفات</returns>
        Task<IEnumerable<ExpenseResponseDto>> GetSupervisorHotelExpensesAsync(
            string hotelCode,
            DateTime fromDate,
            DateTime toDate,
            int? expenseCategoryId = null,
            string? approvalStatus = null);
    }
}
