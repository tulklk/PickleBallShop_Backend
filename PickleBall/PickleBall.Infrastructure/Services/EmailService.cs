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

    public async Task SendPasswordResetEmailAsync(string email, string fullName, string newPassword)
    {
        var subject = "Khôi phục mật khẩu - IKA PickleBall Shop";
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
        .password-box {{ background-color: #ffffff; border: 2px solid #4CAF50; padding: 20px; border-radius: 5px; margin: 20px 0; text-align: center; }}
        .password-text {{ font-size: 24px; font-weight: bold; color: #4CAF50; letter-spacing: 2px; font-family: 'Courier New', monospace; }}
        .warning {{ background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Khôi phục mật khẩu</h1>
        </div>
        <div class='content'>
            <p>Xin chào <strong>{fullName}</strong>,</p>
            <p>Chúng tôi đã nhận được yêu cầu khôi phục mật khẩu cho tài khoản của bạn tại IKA PickleBall Shop.</p>
            <p>Mật khẩu mới của bạn là:</p>
            <div class='password-box'>
                <div class='password-text'>{newPassword}</div>
            </div>
            <div class='warning'>
                <p><strong>⚠️ Lưu ý quan trọng:</strong></p>
                <ul style='margin: 10px 0; padding-left: 20px;'>
                    <li>Vui lòng đăng nhập ngay với mật khẩu mới này</li>
                    <li>Sau khi đăng nhập, hãy đổi mật khẩu thành một mật khẩu dễ nhớ hơn trong phần Cài đặt tài khoản</li>
                    <li>Không chia sẻ mật khẩu này với bất kỳ ai</li>
                    <li>Nếu bạn không yêu cầu khôi phục mật khẩu, vui lòng liên hệ với chúng tôi ngay lập tức</li>
                </ul>
            </div>
            <p>Trân trọng,<br>Đội ngũ IKA PickleBall Shop</p>
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
                ? $"<img src='{itemImage}' alt='{item.ProductNameSnapshot}' class='product-image' />"
                : "<div class='product-image' style='background-color: #f0f0f0; display: flex; align-items: center; justify-content: center;'><span style='color: #999; font-size: 11px;'>No Image</span></div>";
            
            orderItemsHtml += $@"
            <tr style='border-bottom: 1px solid #eee;'>
                <td>
                    <div class='product-item'>
                        {imageHtml}
                        <div class='product-details'>
                            <div class='product-name'>{item.ProductNameSnapshot}</div>
                            <div class='product-quantity'>Số lượng: {item.Quantity}</div>
                        </div>
                        <div class='product-price'>
                            {formatCurrency(item.LineTotal)}
                        </div>
                    </div>
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
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <meta http-equiv='X-UA-Compatible' content='IE=edge'>
    <style>
        * {{ box-sizing: border-box; }}
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 0; width: 100%; }}
        .header {{ background-color: #4CAF50; color: #ffffff; padding: 25px 20px; text-align: center; border-radius: 0; }}
        .header h1 {{ margin: 0; padding: 0; color: #ffffff; font-size: 24px; font-weight: 600; line-height: 1.3; }}
        .content {{ background-color: #ffffff; padding: 20px; border: none; }}
        .order-info {{ background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0; }}
        .order-info-row {{ margin: 12px 0; display: block; }}
        .order-info-label {{ font-weight: 600; color: #666; display: block; margin-bottom: 4px; font-size: 14px; }}
        .order-info-value {{ color: #333; display: block; font-size: 14px; word-break: break-word; }}
        .order-items {{ width: 100%; border-collapse: collapse; margin: 20px 0; display: table; }}
        .order-items th {{ background-color: #f5f5f5; padding: 10px 8px; text-align: left; font-weight: 600; color: #333; border-bottom: 2px solid #ddd; font-size: 14px; }}
        .order-items td {{ padding: 12px 8px; font-size: 14px; }}
        .product-item {{ display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }}
        .product-image {{ width: 70px; height: 70px; object-fit: cover; border-radius: 5px; flex-shrink: 0; }}
        .product-details {{ flex: 1; min-width: 0; }}
        .product-name {{ font-weight: 600; color: #333; margin-bottom: 5px; font-size: 14px; word-break: break-word; }}
        .product-quantity {{ color: #666; font-size: 13px; }}
        .product-price {{ text-align: right; color: #333; font-weight: 600; font-size: 14px; white-space: nowrap; }}
        .button {{ display: inline-block; padding: 14px 25px; background-color: #4CAF50; color: #ffffff !important; text-decoration: none; border-radius: 5px; margin: 10px 5px; font-weight: 600; font-size: 16px; text-align: center; }}
        .button-secondary {{ display: inline-block; padding: 14px 25px; background-color: #2196F3; color: #ffffff !important; text-decoration: none; border-radius: 5px; margin: 10px 5px; font-weight: 600; font-size: 16px; text-align: center; }}
        .footer {{ text-align: center; margin-top: 20px; color: #666; font-size: 12px; padding: 20px; }}
        .total-section {{ background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin-top: 20px; }}
        .total-row {{ display: flex; justify-content: space-between; margin: 10px 0; align-items: center; }}
        .total-label {{ font-weight: 600; color: #666; font-size: 14px; }}
        .total-value {{ font-weight: 600; color: #333; font-size: 18px; }}
        .total-final {{ font-size: 20px; color: #4CAF50; }}
        .support-section {{ margin-top: 30px; padding-top: 20px; border-top: 1px solid #eee; text-align: center; }}
        
        @media only screen and (max-width: 600px) {{
            .container {{ width: 100% !important; padding: 0 !important; }}
            .header {{ padding: 20px 15px !important; }}
            .header h1 {{ font-size: 20px !important; }}
            .content {{ padding: 15px !important; }}
            .order-info {{ padding: 12px !important; margin: 15px 0 !important; }}
            .order-info-row {{ margin: 10px 0 !important; }}
            .order-items {{ font-size: 13px !important; }}
            .order-items th, .order-items td {{ padding: 8px 5px !important; font-size: 13px !important; }}
            .product-item {{ flex-direction: column; align-items: flex-start; }}
            .product-image {{ width: 60px !important; height: 60px !important; }}
            .product-name {{ font-size: 13px !important; }}
            .product-quantity {{ font-size: 12px !important; }}
            .product-price {{ text-align: left !important; margin-top: 5px; width: 100%; }}
            .button, .button-secondary {{ display: block; width: 100%; margin: 10px 0 !important; padding: 12px 20px !important; font-size: 15px !important; }}
            .total-section {{ padding: 12px !important; }}
            .total-label {{ font-size: 13px !important; }}
            .total-value {{ font-size: 16px !important; }}
            .total-final {{ font-size: 18px !important; }}
        }}
    </style>
</head>
<body style='margin: 0; padding: 0; background-color: #f5f5f5;'>
    <div class='container'>
        <div class='header'>
            <h1 style='margin: 0; padding: 0; color: #ffffff;'>Đơn hàng của bạn đã được hoàn thành</h1>
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
            
            <h3 style='color: #333; margin-top: 30px; font-size: 18px; margin-bottom: 15px;'>Chi tiết đơn hàng #{order.OrderNumber}</h3>
            <table class='order-items'>
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
                    <span class='total-label total-final'>Tổng cộng:</span>
                    <span class='total-value total-final'>{formatCurrency(order.TotalAmount)}</span>
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
        
        // Validate email is present and not empty
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            _logger.LogError("Cannot send order confirmation email: Order {OrderId} has no user email. UserId: {UserId}, User: {User}", 
                order.Id, order.UserId, order.User != null ? "exists but no email" : "null");
            throw new InvalidOperationException($"Cannot send order confirmation email: User email is required for order {order.Id}");
        }

        _logger.LogInformation("Preparing to send order confirmation email to: {Email} for order {OrderId}", recipientEmail, order.Id);

        try
        {
            await SendEmailAsync(recipientEmail, subject, body);
            _logger.LogInformation("Order confirmation email sent successfully to: {Email} for order {OrderId}", recipientEmail, order.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send order confirmation email to: {Email} for order {OrderId}. Error: {Error}", 
                recipientEmail, order.Id, ex.Message);
            throw; // Re-throw to let caller handle
        }
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
