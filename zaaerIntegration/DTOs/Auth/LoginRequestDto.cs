using System.ComponentModel.DataAnnotations;

namespace zaaerIntegration.DTOs.Auth
{
    /// <summary>
    /// DTO لطلب تسجيل الدخول
    /// </summary>
    public class LoginRequestDto
    {
        /// <summary>
        /// اسم المستخدم
        /// </summary>
        [Required(ErrorMessage = "Username is required")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// كلمة المرور
        /// </summary>
        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Optional hotel code requested at login. If omitted, the user's default tenant is used.
        /// </summary>
        public string? HotelCode { get; set; }

        /// <summary>
        /// Optional tenant id requested at login.
        /// </summary>
        public int? TenantId { get; set; }

        /// <summary>
        /// Stable client device identifier (generated once per browser).
        /// </summary>
        public string? DeviceId { get; set; }

        /// <summary>
        /// Human-readable device label (e.g. user agent summary).
        /// </summary>
        public string? DeviceName { get; set; }
    }
}

