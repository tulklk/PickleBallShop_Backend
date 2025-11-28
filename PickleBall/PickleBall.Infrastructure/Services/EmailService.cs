using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PickleBall.Application.DTOs.Email;
using PickleBall.Application.Interfaces;
using System.Net;
using System.Net.Mail;

namespace PickleBall.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly EmailSettings _emailSettings;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _emailSettings = new EmailSettings();
        _configuration.GetSection("Email").Bind(_emailSettings);
    }

    public async Task SendVerificationEmailAsync(string email, string fullName, string verificationToken)
    {
        var verificationUrl = $"{_emailSettings.FrontendUrl}/auth/verify-email?token={verificationToken}";
        
        var subject = "Xác nhận email đăng ký tài khoản PickleBall Shop";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4CAF50; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f9f9f9; padding: 30px; border-radius: 0 0 5px 5px; }}
        .button {{ display: inline-block; padding: 12px 30px; background-color: #4CAF50; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Chào mừng đến với PickleBall Shop!</h1>
        </div>
        <div class='content'>
            <p>Xin chào <strong>{fullName}</strong>,</p>
            <p>Cảm ơn bạn đã đăng ký tài khoản tại PickleBall Shop!</p>
            <p>Vui lòng nhấp vào nút bên dưới để xác nhận địa chỉ email của bạn:</p>
            <p style='text-align: center;'>
                <a href='{verificationUrl}' class='button'>Xác nhận Email</a>
            </p>
            <p>Hoặc bạn có thể copy và paste link sau vào trình duyệt:</p>
            <p style='word-break: break-all; color: #4CAF50;'>{verificationUrl}</p>
            <p><strong>Lưu ý:</strong> Link này sẽ hết hạn sau 24 giờ.</p>
            <p>Nếu bạn không yêu cầu tạo tài khoản này, vui lòng bỏ qua email này.</p>
        </div>
        <div class='footer'>
            <p>© {DateTime.UtcNow.Year} PickleBall Shop. Tất cả quyền được bảo lưu.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(email, subject, body);
    }

    public async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
    {
        try
        {
            if (string.IsNullOrEmpty(_emailSettings.SmtpHost) || 
                string.IsNullOrEmpty(_emailSettings.SmtpUsername) || 
                string.IsNullOrEmpty(_emailSettings.SmtpPassword))
            {
                _logger.LogWarning("Email configuration is not set. Email will not be sent. To: {Email}, Subject: {Subject}", toEmail, subject);
                // In development, log the email instead of sending
                _logger.LogInformation("Email would be sent to: {Email}, Subject: {Subject}, Body: {Body}", toEmail, subject, body);
                return true; // Return true to allow development to continue
            }

            using var client = new SmtpClient(_emailSettings.SmtpHost, _emailSettings.SmtpPort)
            {
                EnableSsl = _emailSettings.EnableSsl,
                Credentials = new NetworkCredential(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword)
            };

            using var message = new MailMessage
            {
                From = new MailAddress(_emailSettings.FromEmail, _emailSettings.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            message.To.Add(toEmail);

            await client.SendMailAsync(message);
            _logger.LogInformation("Verification email sent successfully to: {Email}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to: {Email}", toEmail);
            return false;
        }
    }
}

