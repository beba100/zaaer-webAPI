using System.ComponentModel.DataAnnotations;
using zaaerIntegration.Utilities;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// Master User Model - المستخدم في قاعدة البيانات المركزية
    /// </summary>
    public class MasterUser
    {
        /// <summary>
        /// معرف المستخدم
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// اسم المستخدم (Username)
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// كلمة المرور المشفرة (BCrypt Hash)
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// معرف الفندق (Tenant) المرتبط بهذا المستخدم
        /// </summary>
        public int TenantId { get; set; }

        /// <summary>
        /// هل المستخدم نشط
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// تاريخ الإنشاء
        /// </summary>
        public DateTime CreatedAt { get; set; } = KsaTime.UtcNow;

        /// <summary>
        /// تاريخ آخر تحديث
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// RowVersion for Optimistic Concurrency Control
        /// يتم تحديثه تلقائياً عند كل تحديث للصفحة
        /// </summary>
        [Timestamp]
        public byte[]? RowVersion { get; set; }

        /// <summary>
        /// رقم الجوال
        /// </summary>
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// البريد الإلكتروني
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// الرقم الوظيفي
        /// </summary>
        public string? EmployeeNumber { get; set; }

        /// <summary>
        /// الاسم الأول
        /// </summary>
        public string? FirstName { get; set; }

        /// <summary>
        /// اسم العائلة
        /// </summary>
        public string? LastName { get; set; }

        /// <summary>
        /// الاسم الكامل (computed from FirstName + LastName, fallback to FullName property, then Username)
        /// </summary>
        public string? FullName { get; set; }

        /// <summary>
        /// Navigation Properties
        /// </summary>
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        /// <summary>
        /// Navigation property to Tenant
        /// </summary>
        public Tenant? Tenant { get; set; }
    }
}

