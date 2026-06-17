namespace zaaerIntegration.Services.Interfaces
{
    /// <summary>
    /// Interface for Email Service
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// إرسال بريد إلكتروني لإعادة تعيين كلمة المرور
        /// </summary>
        /// <param name="toEmail">البريد الإلكتروني للمستقبل</param>
        /// <param name="toName">اسم المستقبل</param>
        /// <param name="resetToken">الرمز المميز لإعادة التعيين</param>
        /// <param name="resetUrl">رابط إعادة التعيين</param>
        /// <returns>True if email sent successfully</returns>
        Task<bool> SendPasswordResetEmailAsync(string toEmail, string toName, string resetToken, string resetUrl);

        /// <summary>
        /// إرسال بريد إلكتروني عام
        /// </summary>
        /// <param name="toEmail">البريد الإلكتروني للمستقبل</param>
        /// <param name="subject">موضوع البريد</param>
        /// <param name="body">محتوى البريد (HTML)</param>
        /// <returns>True if email sent successfully</returns>
        Task<bool> SendEmailAsync(string toEmail, string subject, string body);
    }
}

