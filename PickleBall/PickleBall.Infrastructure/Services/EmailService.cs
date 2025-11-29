using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PickleBall.Application.DTOs.Email;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Globalization;

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
        .button {{ display: inline-block; padding: 12px 30px; background-color: #4CAF50; color: #ffffff !important; text-decoration: none; border-radius: 5px; margin: 20px 0; font-weight: bold; }}
        .footer {{ text-align: center; margin-top: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Chào mừng đến với IKA PickleBall Shop!</h1>
        </div>
        <div class='content'>
            <p>Xin chào <strong>{fullName}</strong>,</p>
            <p>Cảm ơn bạn đã đăng ký tài khoản tại IKA PickleBall Shop!</p>
            <p>Vui lòng nhấp vào nút bên dưới để xác nhận địa chỉ email của bạn:</p>
            <p style='text-align: center;'>
                <a href='{verificationUrl}' class='button' style='display: inline-block; padding: 12px 30px; background-color: #4CAF50; color: #ffffff !important; text-decoration: none; border-radius: 5px; margin: 20px 0; font-weight: bold;'><span style='color: #ffffff !important;'>Xác nhận Email</span></a>
            </p>
            <p><strong>Lưu ý:</strong> Link này sẽ hết hạn sau 24 giờ.</p>
            <p>Nếu bạn không yêu cầu tạo tài khoản này, vui lòng bỏ qua email này.</p>
        </div>
        <div class='footer'>
            <p>© {DateTime.UtcNow.Year} IKA PickleBall Shop. Tất cả quyền được bảo lưu.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(email, subject, body);
    }

    public async Task SendOrderConfirmationEmailAsync(Order order, string? clientIpAddress = null)
    {
        var orderDetailUrl = $"{_emailSettings.FrontendUrl}/orders/{order.Id}";
        var continueShoppingUrl = _emailSettings.FrontendUrl;
        
        // Format date
        var orderDate = order.CreatedAt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        
        // Format currency
        var formatCurrency = new Func<decimal, string>((amount) => 
            amount.ToString("N0", new CultureInfo("vi-VN")) + " ₫");
        
        // Build order items HTML
        var orderItemsHtml = "";
        foreach (var item in order.Items)
        {
            var itemImage = item.Product?.Images?.FirstOrDefault(i => i.IsPrimary)?.ImageUrl 
                ?? item.Product?.Images?.FirstOrDefault()?.ImageUrl 
                ?? "";
            
            var imageHtml = !string.IsNullOrEmpty(itemImage) 
                ? $"<img src='{itemImage}' alt='{item.ProductNameSnapshot}' style='width: 80px; height: 80px; object-fit: cover; border-radius: 5px;' />"
                : "<div style='width: 80px; height: 80px; background-color: #f0f0f0; border-radius: 5px; display: flex; align-items: center; justify-content: center;'><span style='color: #999;'>No Image</span></div>";
            
            orderItemsHtml += $@"
            <tr style='border-bottom: 1px solid #eee;'>
                <td style='padding: 15px 0;'>
                    <div style='display: flex; align-items: center; gap: 15px;'>
                        {imageHtml}
                        <div>
                            <div style='font-weight: 600; color: #333; margin-bottom: 5px;'>{item.ProductNameSnapshot}</div>
                            <div style='color: #666; font-size: 14px;'>Số lượng: {item.Quantity}</div>
                        </div>
                    </div>
                </td>
                <td style='text-align: right; padding: 15px 0; color: #333; font-weight: 600;'>
                    {formatCurrency(item.LineTotal)}
                </td>
            </tr>";
        }
        
        // Get payment status text
        var paymentStatusText = order.PaymentStatus switch
        {
            PaymentStatus.Paid => "Đã thanh toán",
            PaymentStatus.Pending => "Chờ thanh toán",
            PaymentStatus.Failed => "Thanh toán thất bại",
            _ => "Chưa xác định"
        };
        
        var orderStatusText = order.OrderStatus switch
        {
            OrderStatus.Pending => "Đang chờ xử lý",
            OrderStatus.Confirmed => "Đã xác nhận",
            OrderStatus.Shipping => "Đang giao hàng",
            OrderStatus.Completed => "Hoàn thành",
            OrderStatus.Cancelled => "Đã hủy",
            _ => "Chưa xác định"
        };
        
        var subject = $"Đơn hàng của bạn đã được hoàn thành #{order.OrderNumber}";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4CAF50; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #ffffff; padding: 30px; border: 1px solid #eee; }}
        .order-info {{ background-color: #f9f9f9; padding: 20px; border-radius: 5px; margin: 20px 0; }}
        .order-info-row {{ margin: 10px 0; }}
        .order-info-label {{ font-weight: 600; color: #666; display: inline-block; width: 150px; }}
        .order-info-value {{ color: #333; }}
        .order-items {{ width: 100%; border-collapse: collapse; margin: 20px 0; }}
        .order-items th {{ background-color: #f5f5f5; padding: 12px; text-align: left; font-weight: 600; color: #333; border-bottom: 2px solid #ddd; }}
        .button {{ display: inline-block; padding: 12px 30px; background-color: #4CAF50; color: #ffffff !important; text-decoration: none; border-radius: 5px; margin: 10px 5px; font-weight: bold; }}
        .button-secondary {{ display: inline-block; padding: 12px 30px; background-color: #2196F3; color: #ffffff !important; text-decoration: none; border-radius: 5px; margin: 10px 5px; font-weight: bold; }}
        .footer {{ text-align: center; margin-top: 20px; color: #666; font-size: 12px; padding: 20px; }}
        .total-section {{ background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin-top: 20px; }}
        .total-row {{ display: flex; justify-content: space-between; margin: 10px 0; }}
        .total-label {{ font-weight: 600; color: #666; }}
        .total-value {{ font-weight: 600; color: #333; font-size: 18px; }}
        .support-section {{ margin-top: 30px; padding-top: 20px; border-top: 1px solid #eee; text-align: center; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1 style='margin: 0;'>Đơn hàng của bạn đã được hoàn thành</h1>
        </div>
        <div class='content'>
            <p>Cảm ơn bạn đã quan tâm sản phẩm của IKA PickleBall Shop.</p>
            <p>Xin chào, <strong>{order.User?.FullName ?? order.ShippingFullName}</strong></p>
            
            <p style='text-align: center; margin: 30px 0;'>
                <a href='{orderDetailUrl}' class='button-secondary' style='color: #ffffff !important;'><span style='color: #ffffff !important;'>Xem chi tiết đơn hàng</span></a>
            </p>
            
            <div class='order-info'>
                <h3 style='margin-top: 0; color: #333;'>Thông tin đơn hàng</h3>
                <div class='order-info-row'>
                    <span class='order-info-label'>Ngày đặt hàng:</span>
                    <span class='order-info-value'>{orderDate}</span>
                </div>
                <div class='order-info-row'>
                    <span class='order-info-label'>Khách hàng:</span>
                    <span class='order-info-value'>{order.User?.FullName ?? order.ShippingFullName}</span>
                </div>
                <div class='order-info-row'>
                    <span class='order-info-label'>Email:</span>
                    <span class='order-info-value'>{order.User?.Email ?? ""}</span>
                </div>
                <div class='order-info-row'>
                    <span class='order-info-label'>Số điện thoại:</span>
                    <span class='order-info-value'>{order.ShippingPhoneNumber}</span>
                </div>
                {(string.IsNullOrEmpty(clientIpAddress) ? "" : $@"
                <div class='order-info-row'>
                    <span class='order-info-label'>IP:</span>
                    <span class='order-info-value'>{clientIpAddress}</span>
                </div>")}
                <div class='order-info-row'>
                    <span class='order-info-label'>Email nhận hàng:</span>
                    <span class='order-info-value'>{order.User?.Email ?? ""}</span>
                </div>
                <div class='order-info-row'>
                    <span class='order-info-label'>Tình trạng:</span>
                    <span class='order-info-value'>{orderStatusText}</span>
                </div>
            </div>
            
            <h3 style='color: #333; margin-top: 30px;'>Chi tiết đơn hàng #{order.OrderNumber}</h3>
            <table class='order-items'>
                <thead>
                    <tr>
                        <th style='width: 70%;'>Sản phẩm</th>
                        <th style='width: 30%; text-align: right;'>Giá</th>
                    </tr>
                </thead>
                <tbody>
                    {orderItemsHtml}
                </tbody>
            </table>
            
            <div class='total-section'>
                <div class='total-row'>
                    <span class='total-label'>Tổng giá trị sản phẩm:</span>
                    <span class='total-value'>{formatCurrency(order.TotalAmount)}</span>
                </div>
                <div class='total-row' style='margin-top: 15px; padding-top: 15px; border-top: 2px solid #ddd;'>
                    <span class='total-label' style='font-size: 18px;'>Tổng cộng:</span>
                    <span class='total-value' style='font-size: 20px; color: #4CAF50;'>{formatCurrency(order.TotalAmount)}</span>
                </div>
            </div>
            
            <div class='support-section'>
                <p style='margin: 10px 0; color: #666;'>Liên hệ hỗ trợ</p>
                <p style='margin: 5px 0;'>Nếu bạn có bất kỳ câu hỏi nào, vui lòng liên hệ với chúng tôi.</p>
            </div>
        </div>
        <div class='footer'>
            <p>© {DateTime.UtcNow.Year} IKA PickleBall Shop. Tất cả quyền được bảo lưu.</p>
        </div>
    </div>
</body>
</html>";

        var recipientEmail = order.User?.Email ?? "";
        if (string.IsNullOrEmpty(recipientEmail))
        {
            _logger.LogWarning("Cannot send order confirmation email: Order {OrderId} has no user email", order.Id);
            return;
        }

        await SendEmailAsync(recipientEmail, subject, body);
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
            
            // Set timeout to 30 seconds for connection, read, and write operations
            client.Timeout = 30000; // 30 seconds in milliseconds
            
            try
            {
                _logger.LogInformation("Attempting to connect to SMTP server: {Host}:{Port}", _emailSettings.SmtpHost, _emailSettings.SmtpPort);
                
                // Try Auto first, then fallback to StartTls
                SecureSocketOptions socketOptions = SecureSocketOptions.Auto;
                
                try
                {
                    await client.ConnectAsync(_emailSettings.SmtpHost, _emailSettings.SmtpPort, socketOptions);
                }
                catch
                {
                    // Fallback to StartTls if Auto fails
                    _logger.LogWarning("Auto connection failed, trying StartTls");
                    socketOptions = SecureSocketOptions.StartTls;
                    await client.ConnectAsync(_emailSettings.SmtpHost, _emailSettings.SmtpPort, socketOptions);
                }
                
                _logger.LogInformation("SMTP connection established, authenticating...");
                await client.AuthenticateAsync(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
                
                _logger.LogInformation("SMTP authentication successful, sending email...");
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
                
                _logger.LogInformation("Email sent successfully to: {Email}, Subject: {Subject}", toEmail, subject);
                return true;
            }
            catch (TimeoutException ex)
            {
                _logger.LogError(ex, "SMTP connection timeout to: {Host}:{Port}. Error: {Error}", _emailSettings.SmtpHost, _emailSettings.SmtpPort, ex.Message);
                throw new InvalidOperationException($"SMTP connection timeout. Please check your network connection and SMTP settings.", ex);
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                _logger.LogError(ex, "SMTP socket error connecting to: {Host}:{Port}. Error: {Error}", _emailSettings.SmtpHost, _emailSettings.SmtpPort, ex.Message);
                throw new InvalidOperationException($"Cannot connect to SMTP server. Please check SMTP host and port settings.", ex);
            }
            catch (MailKit.Security.AuthenticationException ex)
            {
                _logger.LogError(ex, "SMTP authentication failed. Error: {Error}", ex.Message);
                throw new InvalidOperationException($"SMTP authentication failed. Please check your username and password (App Password for Gmail).", ex);
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
                    try
                    {
                        await client.DisconnectAsync(true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error disconnecting SMTP client: {Error}", ex.Message);
                    }
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
