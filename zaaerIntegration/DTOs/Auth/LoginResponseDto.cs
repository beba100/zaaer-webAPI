using zaaerIntegration.DTOs.Pms;

namespace zaaerIntegration.DTOs.Auth
{
    /// <summary>
    /// DTO لاستجابة تسجيل الدخول
    /// </summary>
    public class LoginResponseDto
    {
        /// <summary>
        /// JWT Token
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// معرف المستخدم
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// اسم المستخدم
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// الاسم الكامل للمستخدم
        /// </summary>
        public string? FullName { get; set; }

        /// <summary>
        /// الاسم الكامل بالإنجليزية (إن وُجد في قاعدة البيانات).
        /// </summary>
        public string? FullNameEn { get; set; }

        /// <summary>
        /// البريد الإلكتروني
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// رقم الجوال
        /// </summary>
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// الرقم الوظيفي
        /// </summary>
        public string? EmployeeNumber { get; set; }

        /// <summary>
        /// معرف الفندق (Tenant)
        /// </summary>
        public int TenantId { get; set; }

        /// <summary>
        /// كود الفندق
        /// </summary>
        public string TenantCode { get; set; } = string.Empty;

        /// <summary>
        /// اسم الفندق
        /// </summary>
        public string TenantName { get; set; } = string.Empty;

        /// <summary>
        /// اسم الفندق بالإنجليزية.
        /// </summary>
        public string? TenantNameEn { get; set; }

        /// <summary>
        /// أدوار المستخدم
        /// </summary>
        public List<string> Roles { get; set; } = new List<string>();

        /// <summary>
        /// الصلاحيات الفعالة للمستخدم في الفندق الحالي.
        /// </summary>
        public List<string> Permissions { get; set; } = new List<string>();

        /// <summary>
        /// الفنادق المسموح للمستخدم الوصول إليها.
        /// </summary>
        public List<int> AllowedHotelIds { get; set; } = new List<int>();

        /// <summary>
        /// مجموعات الفنادق المسموح للمستخدم الوصول إليها.
        /// </summary>
        public List<int> AllowedGroupIds { get; set; } = new List<int>();

        /// <summary>
        /// مصدر المصادقة للفندق الحالي.
        /// </summary>
        public string AuthMode { get; set; } = "CentralManaged";

        /// <summary>
        /// تاريخ انتهاء Token
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// الفنادق المتاحة للمستخدم (لاختيار الفندق بعد الدخول).
        /// </summary>
        public List<AvailableHotelDto> AvailableHotels { get; set; } = new List<AvailableHotelDto>();

        /// <summary>
        /// Resort ticket gate stations assigned to the current user (from RBAC roles).
        /// </summary>
        public List<PmsResortTicketGateStationDto> GateStations { get; set; } = new();

        /// <summary>
        /// Preferred landing path after login when no returnUrl is provided.
        /// </summary>
        public string? LandingUrl { get; set; }

        /// <summary>
        /// Opaque refresh token (store securely; used to obtain new access tokens).
        /// </summary>
        public string? RefreshToken { get; set; }

        /// <summary>
        /// Refresh token expiry (KSA business time).
        /// </summary>
        public DateTime? RefreshExpiresAt { get; set; }
    }
}

