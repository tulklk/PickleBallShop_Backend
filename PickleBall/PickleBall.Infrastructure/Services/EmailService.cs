using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PickleBall.Application.DTOs.Email;
using PickleBall.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

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
            // Validate email settings
            if (string.IsNullOrEmpty(_emailSettings.SmtpHost))
            {
                _logger.LogError("SMTP Host is not configured");
                throw new InvalidOperationException("Email configuration is incomplete: SMTP Host is missing");
            }

            if (string.IsNullOrEmpty(_emailSettings.SmtpUsername))
            {
                _logger.LogError("SMTP Username is not configured");
                throw new InvalidOperationException("Email configuration is incomplete: SMTP Username is missing");
            }

            if (string.IsNullOrEmpty(_emailSettings.SmtpPassword))
            {
                _logger.LogError("SMTP Password is not configured");
                throw new InvalidOperationException("Email configuration is incomplete: SMTP Password is missing");
            }

            if (_emailSettings.SmtpPort <= 0)
            {
                _logger.LogError("SMTP Port is not configured correctly");
                throw new InvalidOperationException("Email configuration is incomplete: SMTP Port is invalid");
            }

            // Create email message
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_emailSettings.FromName, _emailSettings.FromEmail));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = body
            };
            message.Body = bodyBuilder.ToMessageBody();

            // Send email using MailKit
            using var client = new SmtpClient();
            
            try
            {
                await client.ConnectAsync(_emailSettings.SmtpHost, _emailSettings.SmtpPort, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
                
                _logger.LogInformation("Email sent successfully to: {Email}, Subject: {Subject}", toEmail, subject);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to: {Email}. SMTP Error: {Error}", toEmail, ex.Message);
                throw;
            }
            finally
            {
                if (client.IsConnected)
                {
                    await client.DisconnectAsync(true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to: {Email}", toEmail);
            throw new InvalidOperationException($"Failed to send email: {ex.Message}", ex);
        }
    }
}
