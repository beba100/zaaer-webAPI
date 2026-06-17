using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    /// <summary>
    /// Email Service Implementation using MailKit
    /// </summary>
    public class EmailService : IEmailService
    {
        private static int _incompleteConfigWarningLogged;

        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly string _smtpFromEmail;
        private readonly string _smtpFromName;
        private readonly bool _smtpUseSsl;
        private readonly bool _smtpEnabled;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Read SMTP settings from appsettings.json
            _smtpHost = _configuration["Email:Smtp:Host"] ?? "smtp.gmail.com";
            _smtpPort = int.Parse(_configuration["Email:Smtp:Port"] ?? "587");
            _smtpUsername = _configuration["Email:Smtp:Username"] ?? "";
            _smtpPassword = _configuration["Email:Smtp:Password"] ?? "";
            _smtpFromEmail = _configuration["Email:From:Email"] ?? "noreply@aleairy.com";
            _smtpFromName = _configuration["Email:From:Name"] ?? "فنادق العييري";
            _smtpUseSsl = bool.Parse(_configuration["Email:Smtp:UseSsl"] ?? "true");
            _smtpEnabled = bool.Parse(_configuration["Email:Enabled"] ?? "true");

            if (_smtpEnabled
                && (string.IsNullOrWhiteSpace(_smtpUsername) || string.IsNullOrWhiteSpace(_smtpPassword))
                && Interlocked.CompareExchange(ref _incompleteConfigWarningLogged, 1, 0) == 0)
            {
                _logger.LogWarning(
                    "Email is enabled but SMTP credentials are missing; outbound email is disabled until configured.");
            }
        }

        /// <summary>
        /// إرسال بريد إلكتروني لإعادة تعيين كلمة المرور
        /// </summary>
        public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string toName, string resetToken, string resetUrl)
        {
            if (!_smtpEnabled)
            {
                _logger.LogWarning("⚠️ Email service is disabled. Skipping email send.");
                return false;
            }

            var subject = "إعادة تعيين كلمة المرور - فنادق العييري";
            var body = GeneratePasswordResetEmailBody(toName, resetToken, resetUrl);

            return await SendEmailAsync(toEmail, subject, body);
        }

        /// <summary>
        /// إرسال بريد إلكتروني عام
        /// </summary>
        public async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
        {
            if (!_smtpEnabled)
            {
                _logger.LogWarning("⚠️ Email service is disabled. Skipping email send.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_smtpUsername) || string.IsNullOrWhiteSpace(_smtpPassword))
            {
                _logger.LogError("❌ Email service not configured. Cannot send email.");
                return false;
            }

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_smtpFromName, _smtpFromEmail));
                message.To.Add(new MailboxAddress("", toEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = body
                };
                message.Body = bodyBuilder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    await client.ConnectAsync(_smtpHost, _smtpPort, _smtpUseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
                    await client.AuthenticateAsync(_smtpUsername, _smtpPassword);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }

                _logger.LogInformation("✅ Email sent successfully to: {Email}", toEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to send email to: {Email}", toEmail);
                return false;
            }
        }

        /// <summary>
        /// إنشاء محتوى البريد الإلكتروني لإعادة تعيين كلمة المرور
        /// </summary>
        private string GeneratePasswordResetEmailBody(string userName, string resetToken, string resetUrl)
        {
            return $@"
<!DOCTYPE html>
<html dir='rtl' lang='ar'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f4f4f4;
        }}
        .container {{
            background-color: #ffffff;
            border-radius: 10px;
            padding: 30px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }}
        .header {{
            text-align: center;
            margin-bottom: 30px;
            padding-bottom: 20px;
            border-bottom: 3px solid #00D4AA;
        }}
        .header h1 {{
            color: #1a1a2e;
            margin: 0;
            font-size: 24px;
        }}
        .content {{
            margin: 20px 0;
        }}
        .content p {{
            margin: 15px 0;
            font-size: 16px;
        }}
        .button-container {{
            text-align: center;
            margin: 30px 0;
        }}
        .reset-button {{
            display: inline-block;
            padding: 15px 40px;
            background: linear-gradient(135deg, #00D4AA 0%, #00A8E8 100%);
            color: #ffffff;
            text-decoration: none;
            border-radius: 8px;
            font-weight: 600;
            font-size: 16px;
            box-shadow: 0 4px 15px rgba(0, 212, 170, 0.3);
        }}
        .reset-button:hover {{
            box-shadow: 0 6px 20px rgba(0, 212, 170, 0.4);
        }}
        .token-box {{
            background-color: #f8f9fa;
            border: 2px dashed #00D4AA;
            border-radius: 8px;
            padding: 15px;
            margin: 20px 0;
            text-align: center;
            font-family: 'Courier New', monospace;
            font-size: 18px;
            font-weight: bold;
            color: #1a1a2e;
            word-break: break-all;
        }}
        .warning {{
            background-color: #fff3cd;
            border-right: 4px solid #ffc107;
            padding: 15px;
            margin: 20px 0;
            border-radius: 5px;
        }}
        .warning p {{
            margin: 5px 0;
            color: #856404;
        }}
        .footer {{
            margin-top: 30px;
            padding-top: 20px;
            border-top: 1px solid #e0e0e0;
            text-align: center;
            color: #666;
            font-size: 12px;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>فنادق العييري</h1>
            <p style='color: #00D4AA; margin: 5px 0;'>إعادة تعيين كلمة المرور</p>
        </div>
        
        <div class='content'>
            <p>مرحباً <strong>{userName}</strong>,</p>
            
            <p>لقد تلقينا طلباً لإعادة تعيين كلمة المرور لحسابك في نظام تتبع المصروفات - فنادق العييري.</p>
            
            <p>يمكنك إعادة تعيين كلمة المرور من خلال النقر على الزر أدناه:</p>
            
            <div class='button-container'>
                <a href='{resetUrl}' class='reset-button'>إعادة تعيين كلمة المرور</a>
            </div>
            
            <p>أو يمكنك نسخ الرابط التالي ولصقه في المتصفح:</p>
            <div class='token-box'>{resetUrl}</div>
            
            <div class='warning'>
                <p><strong>⚠️ تحذير:</strong></p>
                <p>• هذا الرابط صالح لمدة <strong>30 دقيقة</strong> فقط</p>
                <p>• إذا لم تطلب إعادة تعيين كلمة المرور، يرجى تجاهل هذا البريد</p>
                <p>• لا تشارك هذا الرابط مع أي شخص آخر</p>
            </div>
            
            <p>إذا لم تطلب إعادة تعيين كلمة المرور، يمكنك تجاهل هذا البريد بأمان.</p>
        </div>
        
        <div class='footer'>
            <p>© {KsaTime.Now.Year} فنادق العييري - جميع الحقوق محفوظة</p>
            <p>هذا بريد إلكتروني تلقائي، يرجى عدم الرد عليه</p>
        </div>
    </div>
</body>
</html>";
        }
    }
}

