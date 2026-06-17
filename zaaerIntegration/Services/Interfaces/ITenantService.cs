using FinanceLedgerAPI.Models;

namespace zaaerIntegration.Services.Interfaces
{
    /// <summary>
    /// خدمة للحصول على معلومات الفندق (Tenant) الحالي من HTTP Request
    /// </summary>
    public interface ITenantService
    {
        /// <summary>
        /// الحصول على الفندق الحالي بناءً على X-Hotel-Code Header
        /// </summary>
        /// <returns>معلومات الفندق أو null إذا لم يتم العثور عليه</returns>
        Tenant? GetTenant();

        /// <summary>
        /// الحصول على كود الفندق من HTTP Request
        /// </summary>
        /// <returns>كود الفندق أو null</returns>
        string? GetTenantCode();

        /// <summary>
        /// الحصول على Connection String للفندق الحالي بناءً على DatabaseName
        /// </summary>
        /// <returns>Connection String للفندق الحالي</returns>
        string GetTenantConnectionString();

        /// <summary>
        /// بناء Connection String لـ Tenant محدد (للاستخدام عند فتح قواعد فنادق متعددة).
        /// </summary>
        string BuildConnectionStringForTenant(Tenant tenant);

        /// <summary>
        /// التحقق من الاتصال بقاعدة بيانات الفندق الحالي
        /// </summary>
        /// <returns>true إذا كان الاتصال ناجحاً</returns>
        Task<bool> ValidateTenantConnectionAsync();

        /// <summary>
        /// تعيين الـ Tenant الحالي مباشرة (للاستخدام في background workers حيث لا يوجد HttpContext)
        /// Set the current tenant directly (for use in background workers where HttpContext is not available)
        /// </summary>
        /// <param name="tenant">الـ Tenant المراد تعيينه</param>
        void SetCurrentTenant(Tenant tenant);

        /// <summary>
        /// الحصول على الـ Tenant من HotelId (للاستخدام في background workers)
        /// Get tenant from HotelId (for use in background workers)
        /// </summary>
        /// <param name="hotelId">Hotel ID (Zaaer ID from hotel_settings)</param>
        /// <returns>Tenant information</returns>
        Task<Tenant?> GetTenantByHotelIdAsync(int hotelId);
    }
}

