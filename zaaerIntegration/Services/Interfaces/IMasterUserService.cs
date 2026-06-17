using FinanceLedgerAPI.Models;

namespace zaaerIntegration.Services.Interfaces
{
    /// <summary>
    /// Interface for Master User Service
    /// </summary>
    public interface IMasterUserService
    {
        /// <summary>
        /// الحصول على المستخدم بواسطة Username
        /// </summary>
        Task<MasterUser?> GetByUsernameAsync(string username);

        /// <summary>
        /// الحصول على المستخدم بواسطة EmployeeNumber
        /// </summary>
        Task<MasterUser?> GetByEmployeeNumberAsync(string employeeNumber);

        /// <summary>
        /// الحصول على المستخدم بواسطة Id
        /// </summary>
        Task<MasterUser?> GetByIdAsync(int userId);

        /// <summary>
        /// الحصول على أدوار المستخدم
        /// </summary>
        Task<IEnumerable<string>> GetUserRolesAsync(int userId);

        /// <summary>
        /// التحقق من كلمة المرور
        /// </summary>
        bool ValidatePassword(string password, string passwordHash);

        /// <summary>
        /// تشفير كلمة المرور
        /// </summary>
        string HashPassword(string password);

        /// <summary>
        /// إنشاء مستخدم جديد
        /// </summary>
        Task<MasterUser> CreateUserAsync(string username, string password, int tenantId, IEnumerable<int> roleIds);

        /// <summary>
        /// إنشاء مستخدم جديد مع الحقول الإضافية
        /// </summary>
        Task<MasterUser> CreateUserAsync(string username, string password, int tenantId, IEnumerable<int> roleIds, 
            string? phoneNumber, string? email, string? employeeNumber, string? fullName, 
            IEnumerable<int>? additionalTenantIds = null);

        /// <summary>
        /// الحصول على جميع المستخدمين
        /// </summary>
        Task<IEnumerable<MasterUser>> GetAllUsersAsync();

        /// <summary>
        /// تحديث مستخدم
        /// </summary>
        Task<MasterUser> UpdateUserAsync(int userId, string? username, string? password, int? tenantId, 
            string? phoneNumber, string? email, string? employeeNumber, string? fullName, 
            bool? isActive, IEnumerable<int>? roleIds, IEnumerable<int>? additionalTenantIds);

        /// <summary>
        /// حذف مستخدم
        /// </summary>
        Task<bool> DeleteUserAsync(int userId);

        /// <summary>
        /// التحقق من صحة بيانات تسجيل الدخول باستخدام الرقم الوظيفي (EmployeeNumber)
        /// </summary>
        /// <param name="employeeNumber">الرقم الوظيفي للمستخدم</param>
        /// <param name="password">كلمة المرور</param>
        Task<MasterUser?> ValidateLoginAsync(string employeeNumber, string password);

        /// <summary>
        /// تغيير كلمة المرور مع Concurrency Control
        /// ✅ Senior Level: Handles multiple users changing passwords simultaneously
        /// Uses database-level locking and optimistic concurrency control
        /// </summary>
        /// <param name="userId">معرف المستخدم</param>
        /// <param name="currentPassword">كلمة المرور الحالية</param>
        /// <param name="newPassword">كلمة المرور الجديدة</param>
        /// <returns>True if password changed successfully, False if current password is incorrect</returns>
        /// <exception cref="KeyNotFoundException">User not found</exception>
        /// <exception cref="InvalidOperationException">User is inactive or concurrency conflict</exception>
        Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);

        /// <summary>
        /// إنشاء رمز إعادة تعيين كلمة المرور
        /// </summary>
        /// <param name="userId">معرف المستخدم</param>
        /// <param name="ipAddress">عنوان IP للطلب</param>
        /// <returns>الرمز المميز (Token)</returns>
        Task<string> CreatePasswordResetTokenAsync(int userId, string? ipAddress = null);

        /// <summary>
        /// التحقق من صحة رمز إعادة التعيين
        /// </summary>
        /// <param name="token">الرمز المميز</param>
        /// <returns>معرف المستخدم إذا كان الرمز صالحاً، null إذا كان غير صالح</returns>
        Task<int?> ValidateResetTokenAsync(string token);

        /// <summary>
        /// إعادة تعيين كلمة المرور باستخدام الرمز المميز
        /// </summary>
        /// <param name="token">الرمز المميز</param>
        /// <param name="newPassword">كلمة المرور الجديدة</param>
        /// <returns>True if password reset successfully</returns>
        Task<bool> ResetPasswordAsync(string token, string newPassword);

        /// <summary>
        /// الحصول على المستخدم بواسطة البريد الإلكتروني
        /// </summary>
        Task<MasterUser?> GetByEmailAsync(string email);

        /// <summary>
        /// الحصول على عدد طلبات إعادة التعيين الأخيرة (للمنع من الإساءة)
        /// </summary>
        Task<int> GetRecentResetRequestsAsync(int userId, TimeSpan timeWindow);
    }
}

