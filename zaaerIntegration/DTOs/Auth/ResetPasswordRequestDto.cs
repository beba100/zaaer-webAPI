using System.ComponentModel.DataAnnotations;

namespace zaaerIntegration.DTOs.Auth
{
    /// <summary>
    /// DTO لإعادة تعيين كلمة المرور
    /// </summary>
    public class ResetPasswordRequestDto
    {
        /// <summary>
        /// الرمز المميز (Token) المرسل عبر البريد الإلكتروني
        /// </summary>
        [Required(ErrorMessage = "Token is required")]
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// كلمة المرور الجديدة
        /// </summary>
        [Required(ErrorMessage = "New password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string NewPassword { get; set; } = string.Empty;

        /// <summary>
        /// تأكيد كلمة المرور الجديدة
        /// </summary>
        [Required(ErrorMessage = "Password confirmation is required")]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}

