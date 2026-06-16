using System.ComponentModel.DataAnnotations;

namespace zaaerIntegration.DTOs.Auth
{
    /// <summary>
    /// DTO لطلب إعادة تعيين كلمة المرور
    /// </summary>
    public class ForgotPasswordRequestDto
    {
        /// <summary>
        /// اسم المستخدم أو البريد الإلكتروني
        /// </summary>
        [Required(ErrorMessage = "Username or email is required")]
        public string UsernameOrEmail { get; set; } = string.Empty;
    }
}

