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
                ? $"<img src='{itemImage}' alt='{item.ProductNameSnapshot}' class='product-image' style='width: 80px; height: 80px; object-fit: cover; border-radius: 8px; border: 1px solid #e0e0e0;' />"
                : "<div class='product-image' style='width: 80px; height: 80px; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); display: flex; align-items: center; justify-content: center; border-radius: 8px;'><span style='color: #fff; font-size: 24px;'>🏓</span></div>";
            
            var variantInfo = !string.IsNullOrEmpty(item.VariantInfoSnapshot) 
                ? $"<div class='product-variant' style='color: #666; font-size: 13px; margin-top: 4px; padding: 4px 8px; background-color: #f8f9fa; border-radius: 4px; display: inline-block;'>{item.VariantInfoSnapshot}</div>"
                : "";
            
            orderItemsHtml += $@"
            <tr style='border-bottom: 1px solid #e8e8e8;'>
                <td style='padding: 20px 0;'>
                    <div class='product-item' style='display: flex; align-items: flex-start; gap: 16px;'>
                        <div class='product-image-wrapper'>
                            {imageHtml}
                        </div>
                        <div class='product-details' style='flex: 1; min-width: 0;'>
                            <div class='product-name' style='font-weight: 600; color: #1a1a1a; margin-bottom: 8px; font-size: 15px; line-height: 1.4;'>{item.ProductNameSnapshot}</div>
                            {variantInfo}
                            <div class='product-meta' style='color: #888; font-size: 13px; margin-top: 8px;'>
                                Số lượng: <strong style='color: #333;'>{item.Quantity}</strong> × {formatCurrency(item.UnitPrice)}
                            </div>
                        </div>
                        <div class='product-price-wrapper' style='text-align: right;'>
                            <div class='product-price' style='color: #1a1a1a; font-weight: 700; font-size: 18px; white-space: nowrap;'>
                                {formatCurrency(item.LineTotal)}
                            </div>
                        </div>
                    </div>
                </td>
            </tr>";
        }
        
        // Get payment status text with badge
        var (paymentStatusText, paymentStatusClass) = order.PaymentStatus switch
        {
            PaymentStatus.Paid => ("✓ Đã thanh toán", "status-badge-success"),
            PaymentStatus.Pending => ("⏳ Chờ thanh toán", "status-badge-warning"),
            PaymentStatus.Failed => ("✗ Thanh toán thất bại", "status-badge-error"),
            _ => ("❓ Chưa xác định", "status-badge-default")
        };
        
        var (orderStatusText, orderStatusClass) = order.OrderStatus switch
        {
            OrderStatus.Processing => ("⏳ Đang xử lý", "status-badge-warning"),
            OrderStatus.Confirmed => ("✓ Đã xác nhận", "status-badge-success"),
            OrderStatus.Prepared => ("📦 Đã chuẩn bị hàng", "status-badge-info"),
            OrderStatus.Shipped => ("🚚 Đã giao cho đơn vị vận chuyển", "status-badge-info"),
            OrderStatus.Completed => ("✅ Hoàn thành", "status-badge-success"),
            OrderStatus.Cancelled => ("✗ Đã hủy", "status-badge-error"),
            _ => ("❓ Chưa xác định", "status-badge-default")
        };

        var paymentMethodText = order.PaymentMethod switch
        {
            PaymentMethod.COD => "💰 Thanh toán khi nhận hàng (COD)",
            PaymentMethod.BankTransfer => "🏦 Chuyển khoản ngân hàng",
            PaymentMethod.MoMo => "💜 Ví MoMo",
            PaymentMethod.VNPay => "💳 VNPay",
            _ => "❓ Chưa xác định"
        };
        
        var shippingAddress = $"{order.ShippingAddressLine}, {order.ShippingWard}, {order.ShippingDistrict}, {order.ShippingCity}".TrimStart(',', ' ').Trim();
        
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
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Roboto', 'Helvetica Neue', Arial, sans-serif; line-height: 1.6; color: #1a1a1a; margin: 0; padding: 0; -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%; background-color: #f5f7fa; }}
        .email-wrapper {{ background-color: #f5f7fa; padding: 20px 0; }}
        .container {{ max-width: 600px; margin: 0 auto; width: 100%; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.1); }}
        .header {{ background: linear-gradient(135deg, #4CAF50 0%, #45a049 100%); color: #ffffff; padding: 40px 30px; text-align: center; position: relative; }}
        .header::after {{ content: ''; position: absolute; bottom: 0; left: 0; right: 0; height: 4px; background: linear-gradient(90deg, #4CAF50, #45a049, #4CAF50); }}
        .header h1 {{ margin: 0; padding: 0; color: #ffffff; font-size: 26px; font-weight: 700; line-height: 1.3; letter-spacing: -0.5px; }}
        .header-icon {{ font-size: 48px; margin-bottom: 10px; display: block; }}
        .content {{ background-color: #ffffff; padding: 30px; }}
        .greeting {{ font-size: 16px; color: #333; margin-bottom: 25px; line-height: 1.6; }}
        .greeting strong {{ color: #1a1a1a; font-size: 18px; }}
        .cta-section {{ text-align: center; margin: 35px 0; }}
        .button {{ display: inline-block; padding: 16px 32px; background: linear-gradient(135deg, #2196F3 0%, #1976D2 100%); color: #ffffff !important; text-decoration: none; border-radius: 8px; margin: 8px; font-weight: 600; font-size: 16px; text-align: center; box-shadow: 0 4px 12px rgba(33, 150, 243, 0.3); transition: all 0.3s ease; }}
        .button:hover {{ box-shadow: 0 6px 16px rgba(33, 150, 243, 0.4); transform: translateY(-2px); }}
        .info-section {{ background: linear-gradient(135deg, #f8f9fa 0%, #ffffff 100%); padding: 25px; border-radius: 10px; margin: 30px 0; border: 1px solid #e8e8e8; }}
        .info-section h3 {{ margin: 0 0 20px 0; color: #1a1a1a; font-size: 20px; font-weight: 700; padding-bottom: 15px; border-bottom: 2px solid #4CAF50; }}
        .info-grid {{ display: grid; gap: 16px; }}
        .info-row {{ display: flex; flex-direction: column; padding: 12px 0; border-bottom: 1px solid #f0f0f0; }}
        .info-row:last-child {{ border-bottom: none; }}
        .info-label {{ font-weight: 600; color: #666; font-size: 13px; text-transform: uppercase; letter-spacing: 0.5px; margin-bottom: 6px; }}
        .info-value {{ color: #1a1a1a; font-size: 15px; word-break: break-word; line-height: 1.5; }}
        .status-badge {{ display: inline-block; padding: 6px 14px; border-radius: 20px; font-size: 13px; font-weight: 600; margin-top: 4px; }}
        .status-badge-success {{ background-color: #d4edda; color: #155724; }}
        .status-badge-warning {{ background-color: #fff3cd; color: #856404; }}
        .status-badge-error {{ background-color: #f8d7da; color: #721c24; }}
        .status-badge-info {{ background-color: #d1ecf1; color: #0c5460; }}
        .status-badge-default {{ background-color: #e2e3e5; color: #383d41; }}
        .order-details-section {{ margin: 35px 0; }}
        .section-title {{ color: #1a1a1a; font-size: 20px; font-weight: 700; margin-bottom: 20px; padding-bottom: 12px; border-bottom: 3px solid #4CAF50; display: flex; align-items: center; gap: 10px; }}
        .section-title::before {{ content: '📦'; font-size: 24px; }}
        .order-items-table {{ width: 100%; border-collapse: collapse; margin: 20px 0; background-color: #ffffff; }}
        .order-items-table td {{ padding: 0; }}
        .product-item {{ display: flex; align-items: flex-start; gap: 16px; padding: 20px 0; }}
        .product-image-wrapper {{ flex-shrink: 0; }}
        .product-details {{ flex: 1; min-width: 0; }}
        .product-name {{ font-weight: 600; color: #1a1a1a; margin-bottom: 8px; font-size: 15px; line-height: 1.4; }}
        .product-variant {{ color: #666; font-size: 13px; margin-top: 4px; padding: 4px 8px; background-color: #f8f9fa; border-radius: 4px; display: inline-block; }}
        .product-meta {{ color: #888; font-size: 13px; margin-top: 6px; }}
        .product-meta strong {{ color: #333; }}
        .product-price-wrapper {{ text-align: right; }}
        .product-price {{ color: #1a1a1a; font-weight: 700; font-size: 18px; white-space: nowrap; }}
        .product-unit-price {{ color: #999; font-size: 12px; margin-top: 4px; }}
        .total-section {{ background: linear-gradient(135deg, #f8f9fa 0%, #ffffff 100%); padding: 25px; border-radius: 10px; margin-top: 30px; border: 2px solid #e8e8e8; }}
        .total-row {{ display: flex; justify-content: space-between; align-items: center; margin: 12px 0; }}
        .total-label {{ font-weight: 600; color: #666; font-size: 15px; }}
        .total-value {{ font-weight: 600; color: #1a1a1a; font-size: 18px; }}
        .total-final-row {{ margin-top: 20px; padding-top: 20px; border-top: 3px solid #4CAF50; }}
        .total-final-label {{ font-size: 18px; font-weight: 700; color: #1a1a1a; }}
        .total-final-value {{ font-size: 24px; font-weight: 700; color: #4CAF50; }}
        .support-section {{ margin-top: 40px; padding: 25px; background-color: #f8f9fa; border-radius: 10px; text-align: center; border: 1px solid #e8e8e8; }}
        .support-section h4 {{ margin: 0 0 10px 0; color: #1a1a1a; font-size: 18px; font-weight: 600; }}
        .support-section p {{ margin: 5px 0; color: #666; font-size: 14px; }}
        .footer {{ text-align: center; margin-top: 30px; padding: 25px; background-color: #f8f9fa; color: #666; font-size: 12px; border-top: 1px solid #e8e8e8; }}
        .footer p {{ margin: 5px 0; }}
        .divider {{ height: 1px; background: linear-gradient(90deg, transparent, #e0e0e0, transparent); margin: 25px 0; }}
        
        @media only screen and (max-width: 600px) {{
            .email-wrapper {{ padding: 10px !important; }}
            .container {{ border-radius: 8px !important; }}
            .header {{ padding: 30px 20px !important; }}
            .header h1 {{ font-size: 22px !important; }}
            .content {{ padding: 20px !important; }}
            .info-section {{ padding: 20px !important; margin: 20px 0 !important; }}
            .info-section h3 {{ font-size: 18px !important; }}
            .section-title {{ font-size: 18px !important; }}
            .product-item {{ flex-direction: column; gap: 12px !important; }}
            .product-image-wrapper {{ width: 100%; text-align: center; }}
            .product-price-wrapper {{ text-align: left !important; width: 100%; }}
            .product-price {{ font-size: 16px !important; }}
            .button {{ display: block; width: 100%; margin: 10px 0 !important; padding: 14px 24px !important; }}
            .total-section {{ padding: 20px !important; }}
            .total-label, .total-value {{ font-size: 14px !important; }}
            .total-final-label {{ font-size: 16px !important; }}
            .total-final-value {{ font-size: 20px !important; }}
        }}
    </style>
</head>
<body style='margin: 0; padding: 0; background-color: #f5f7fa;'>
    <div class='email-wrapper'>
        <div class='container'>
            <div class='header'>
                <span class='header-icon'>✅</span>
                <h1>Đơn hàng của bạn đã được hoàn thành</h1>
                <p style='margin: 15px 0 0 0; font-size: 16px; opacity: 0.95;'>Mã đơn hàng: <strong>#{order.OrderNumber}</strong></p>
            </div>
            
            <div class='content'>
                <div class='greeting'>
                    <p style='margin: 0 0 10px 0;'>Xin chào <strong>{order.User?.FullName ?? order.ShippingFullName}</strong>,</p>
                    <p style='margin: 0; color: #666;'>Cảm ơn bạn đã đặt hàng tại <strong style='color: #4CAF50;'>IKA PickleBall Shop</strong>! Chúng tôi rất vui được phục vụ bạn.</p>
                </div>
                
                <div class='cta-section'>
                    <a href='{orderDetailUrl}' class='button'>Xem chi tiết đơn hàng →</a>
                </div>
                
                <div class='info-section'>
                    <h3>📋 Thông tin đơn hàng</h3>
                    <div class='info-grid'>
                        <div class='info-row'>
                            <span class='info-label'>Mã đơn hàng</span>
                            <span class='info-value'><strong>#{order.OrderNumber}</strong></span>
                        </div>
                        <div class='info-row'>
                            <span class='info-label'>Ngày đặt hàng</span>
                            <span class='info-value'>{orderDate}</span>
                        </div>
                        <div class='info-row'>
                            <span class='info-label'>Khách hàng</span>
                            <span class='info-value'>{order.User?.FullName ?? order.ShippingFullName}</span>
                        </div>
                        <div class='info-row'>
                            <span class='info-label'>Email</span>
                            <span class='info-value'>{order.User?.Email ?? ""}</span>
                        </div>
                        <div class='info-row'>
                            <span class='info-label'>Số điện thoại</span>
                            <span class='info-value'>{order.ShippingPhoneNumber}</span>
                        </div>
                        <div class='info-row'>
                            <span class='info-label'>Địa chỉ giao hàng</span>
                            <span class='info-value'>{shippingAddress}</span>
                        </div>
                        <div class='info-row'>
                            <span class='info-label'>Phương thức thanh toán</span>
                            <span class='info-value'>{paymentMethodText}</span>
                        </div>
                        <div class='info-row'>
                            <span class='info-label'>Trạng thái thanh toán</span>
                            <span class='info-value'><span class='status-badge {paymentStatusClass}'>{paymentStatusText}</span></span>
                        </div>
                        <div class='info-row'>
                            <span class='info-label'>Trạng thái đơn hàng</span>
                            <span class='info-value'><span class='status-badge {orderStatusClass}'>{orderStatusText}</span></span>
                        </div>
                    </div>
                </div>
                
                <div class='order-details-section'>
                    <div class='section-title'>Chi tiết sản phẩm</div>
                    <table class='order-items-table'>
                        <tbody>
                            {orderItemsHtml}
                        </tbody>
                    </table>
                    
                    <div class='total-section'>
                        <div class='total-row'>
                            <span class='total-label'>Tổng giá trị sản phẩm</span>
                            <span class='total-value'>{formatCurrency(order.TotalAmount)}</span>
                        </div>
                        <div class='total-row total-final-row'>
                            <span class='total-label total-final-label'>Tổng cộng</span>
                            <span class='total-value total-final-value'>{formatCurrency(order.TotalAmount)}</span>
                        </div>
                    </div>
                </div>
                
                <div class='support-section'>
                    <h4>💬 Cần hỗ trợ?</h4>
                    <p>Nếu bạn có bất kỳ câu hỏi nào về đơn hàng, vui lòng liên hệ với chúng tôi qua email hoặc số điện thoại hỗ trợ.</p>
                    <p style='margin-top: 15px;'><a href='{continueShoppingUrl}' style='color: #4CAF50; text-decoration: none; font-weight: 600;'>Tiếp tục mua sắm →</a></p>
                </div>
            </div>
            
            <div class='footer'>
                <p style='margin: 0 0 10px 0;'><strong>IKA PickleBall Shop</strong></p>
                <p style='margin: 0;'>© {DateTime.UtcNow.Year} IKA PickleBall Shop. Tất cả quyền được bảo lưu.</p>
            </div>
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

    public async Task SendGuestOrderConfirmationEmailAsync(Order order, string? clientIpAddress = null)
    {
        var orderDetailUrl = $"{_emailSettings.FrontendUrl}/guest/orders/{order.Id}?email={Uri.EscapeDataString(order.GuestEmail ?? "")}";
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
                ? $"<img src='{itemImage}' alt='{item.ProductNameSnapshot}' class='product-image' style='width: 80px; height: 80px; object-fit: cover; border-radius: 8px; border: 1px solid #e0e0e0;' />"
                : "<div class='product-image' style='width: 80px; height: 80px; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); display: flex; align-items: center; justify-content: center; border-radius: 8px;'><span style='color: #fff; font-size: 24px;'>🏓</span></div>";
            
            var variantInfo = !string.IsNullOrEmpty(item.VariantInfoSnapshot) 
                ? $"<div class='product-variant' style='color: #666; font-size: 13px; margin-top: 4px; padding: 4px 8px; background-color: #f8f9fa; border-radius: 4px; display: inline-block;'>{item.VariantInfoSnapshot}</div>"
                : "";
            
            orderItemsHtml += $@"
            <tr style='border-bottom: 1px solid #e8e8e8;'>
                <td style='padding: 20px 0;'>
                    <div class='product-item' style='display: flex; align-items: flex-start; gap: 16px;'>
                        <div class='product-image-wrapper'>
                            {imageHtml}
                        </div>
                        <div class='product-details' style='flex: 1; min-width: 0;'>
                            <div class='product-name' style='font-weight: 600; color: #1a1a1a; margin-bottom: 8px; font-size: 15px; line-height: 1.4;'>{item.ProductNameSnapshot}</div>
                            {variantInfo}
                            <div class='product-meta' style='color: #888; font-size: 13px; margin-top: 8px;'>
                                Số lượng: <strong style='color: #333;'>{item.Quantity}</strong> × {formatCurrency(item.UnitPrice)}
                            </div>
                        </div>
                        <div class='product-price-wrapper' style='text-align: right;'>
                            <div class='product-price' style='color: #1a1a1a; font-weight: 700; font-size: 18px; white-space: nowrap;'>
                                {formatCurrency(item.LineTotal)}
                            </div>
                        </div>
                    </div>
                </td>
            </tr>";
        }
        
        // Get payment status text with badge
        var (paymentStatusText, paymentStatusClass) = order.PaymentStatus switch
        {
            PaymentStatus.Paid => ("✓ Đã thanh toán", "status-badge-success"),
            PaymentStatus.Pending => ("⏳ Chờ thanh toán", "status-badge-warning"),
            PaymentStatus.Failed => ("✗ Thanh toán thất bại", "status-badge-error"),
            _ => ("❓ Chưa xác định", "status-badge-default")
        };
        
        var (orderStatusText, orderStatusClass) = order.OrderStatus switch
        {
            OrderStatus.Processing => ("⏳ Đang xử lý", "status-badge-warning"),
            OrderStatus.Confirmed => ("✓ Đã xác nhận", "status-badge-success"),
            OrderStatus.Prepared => ("📦 Đã chuẩn bị hàng", "status-badge-info"),
            OrderStatus.Shipped => ("🚚 Đã giao cho đơn vị vận chuyển", "status-badge-info"),
            OrderStatus.Completed => ("✅ Hoàn thành", "status-badge-success"),
            OrderStatus.Cancelled => ("✗ Đã hủy", "status-badge-error"),
            _ => ("❓ Chưa xác định", "status-badge-default")
        };
        
        var paymentMethodText = order.PaymentMethod switch
        {
            PaymentMethod.COD => "💰 Thanh toán khi nhận hàng (COD)",
            PaymentMethod.BankTransfer => "🏦 Chuyển khoản ngân hàng",
            PaymentMethod.MoMo => "💜 Ví MoMo",
            PaymentMethod.VNPay => "💳 VNPay",
            _ => "❓ Chưa xác định"
        };
        
        var shippingAddress = $"{order.ShippingAddressLine}, {order.ShippingWard}, {order.ShippingDistrict}, {order.ShippingCity}".TrimStart(',', ' ').Trim();
        
        var subject = $"Xác nhận đơn hàng #{order.OrderNumber} - IKA PickleBall Shop";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <meta http-equiv='X-UA-Compatible' content='IE=edge'>
    <style>
        * {{ box-sizing: border-box; }}
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Roboto', 'Helvetica Neue', Arial, sans-serif; line-height: 1.6; color: #1a1a1a; margin: 0; padding: 0; -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%; background-color: #f5f7fa; }}
        .email-wrapper {{ background-color: #f5f7fa; padding: 20px 0; }}
        .container {{ max-width: 600px; margin: 0 auto; width: 100%; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.1); }}
        .header {{ background: linear-gradient(135deg, #4CAF50 0%, #45a049 100%); color: #ffffff; padding: 40px 30px; text-align: center; position: relative; }}
        .header::after {{ content: ''; position: absolute; bottom: 0; left: 0; right: 0; height: 4px; background: linear-gradient(90deg, #4CAF50, #45a049, #4CAF50); }}
        .header h1 {{ margin: 0; padding: 0; color: #ffffff; font-size: 26px; font-weight: 700; line-height: 1.3; letter-spacing: -0.5px; }}
        .header-icon {{ font-size: 48px; margin-bottom: 10px; display: block; }}
        .content {{ background-color: #ffffff; padding: 30px; }}
        .greeting {{ font-size: 16px; color: #333; margin-bottom: 25px; line-height: 1.6; }}
        .greeting strong {{ color: #1a1a1a; font-size: 18px; }}
        .cta-section {{ text-align: center; margin: 35px 0; }}
        .button {{ display: inline-block; padding: 16px 32px; background: linear-gradient(135deg, #2196F3 0%, #1976D2 100%); color: #ffffff !important; text-decoration: none; border-radius: 8px; margin: 8px; font-weight: 600; font-size: 16px; text-align: center; box-shadow: 0 4px 12px rgba(33, 150, 243, 0.3); transition: all 0.3s ease; }}
        .button:hover {{ box-shadow: 0 6px 16px rgba(33, 150, 243, 0.4); transform: translateY(-2px); }}
        .info-section {{ background: linear-gradient(135deg, #f8f9fa 0%, #ffffff 100%); padding: 25px; border-radius: 10px; margin: 30px 0; border: 1px solid #e8e8e8; }}
        .info-section h3 {{ margin: 0 0 20px 0; color: #1a1a1a; font-size: 20px; font-weight: 700; padding-bottom: 15px; border-bottom: 2px solid #4CAF50; }}
        .info-grid {{ display: grid; gap: 16px; }}
        .info-row {{ display: flex; flex-direction: column; padding: 12px 0; border-bottom: 1px solid #f0f0f0; }}
        .info-row:last-child {{ border-bottom: none; }}
        .info-label {{ font-weight: 600; color: #666; font-size: 13px; text-transform: uppercase; letter-spacing: 0.5px; margin-bottom: 6px; }}
        .info-value {{ color: #1a1a1a; font-size: 15px; word-break: break-word; line-height: 1.5; }}
        .status-badge {{ display: inline-block; padding: 6px 14px; border-radius: 20px; font-size: 13px; font-weight: 600; margin-top: 4px; }}
        .status-badge-success {{ background-color: #d4edda; color: #155724; }}
        .status-badge-warning {{ background-color: #fff3cd; color: #856404; }}
        .status-badge-error {{ background-color: #f8d7da; color: #721c24; }}
        .status-badge-info {{ background-color: #d1ecf1; color: #0c5460; }}
        .status-badge-default {{ background-color: #e2e3e5; color: #383d41; }}
        .order-details-section {{ margin: 35px 0; }}
        .section-title {{ color: #1a1a1a; font-size: 20px; font-weight: 700; margin-bottom: 20px; padding-bottom: 12px; border-bottom: 3px solid #4CAF50; display: flex; align-items: center; gap: 10px; }}
        .section-title::before {{ content: '📦'; font-size: 24px; }}
        .order-items-table {{ width: 100%; border-collapse: collapse; margin: 20px 0; background-color: #ffffff; }}
        .order-items-table td {{ padding: 0; }}
        .product-item {{ display: flex; align-items: flex-start; gap: 16px; padding: 20px 0; }}
        .product-image-wrapper {{ flex-shrink: 0; }}
        .product-details {{ flex: 1; min-width: 0; }}
        .product-name {{ font-weight: 600; color: #1a1a1a; margin-bottom: 8px; font-size: 15px; line-height: 1.4; }}
        .product-variant {{ color: #666; font-size: 13px; margin-top: 4px; padding: 4px 8px; background-color: #f8f9fa; border-radius: 4px; display: inline-block; }}
        .product-meta {{ color: #888; font-size: 13px; margin-top: 6px; }}
        .product-meta strong {{ color: #333; }}
        .product-price-wrapper {{ text-align: right; }}
        .product-price {{ color: #1a1a1a; font-weight: 700; font-size: 18px; white-space: nowrap; }}
        .product-unit-price {{ color: #999; font-size: 12px; margin-top: 4px; }}
        .total-section {{ background: linear-gradient(135deg, #f8f9fa 0%, #ffffff 100%); padding: 25px; border-radius: 10px; margin-top: 30px; border: 2px solid #e8e8e8; }}
        .total-row {{ display: flex; justify-content: space-between; align-items: center; margin: 12px 0; }}
        .total-label {{ font-weight: 600; color: #666; font-size: 15px; }}
        .total-value {{ font-weight: 600; color: #1a1a1a; font-size: 18px; }}
        .total-final-row {{ margin-top: 20px; padding-top: 20px; border-top: 3px solid #4CAF50; }}
        .total-final-label {{ font-size: 18px; font-weight: 700; color: #1a1a1a; }}
        .total-final-value {{ font-size: 24px; font-weight: 700; color: #4CAF50; }}
        .support-section {{ margin-top: 40px; padding: 25px; background-color: #f8f9fa; border-radius: 10px; text-align: center; border: 1px solid #e8e8e8; }}
        .support-section h4 {{ margin: 0 0 10px 0; color: #1a1a1a; font-size: 18px; font-weight: 600; }}
        .support-section p {{ margin: 5px 0; color: #666; font-size: 14px; }}
        .footer {{ text-align: center; margin-top: 30px; padding: 25px; background-color: #f8f9fa; color: #666; font-size: 12px; border-top: 1px solid #e8e8e8; }}
        .footer p {{ margin: 5px 0; }}
        .divider {{ height: 1px; background: linear-gradient(90deg, transparent, #e0e0e0, transparent); margin: 25px 0; }}
        
        @media only screen and (max-width: 600px) {{
            .email-wrapper {{ padding: 10px !important; }}
            .container {{ border-radius: 8px !important; }}
            .header {{ padding: 30px 20px !important; }}
            .header h1 {{ font-size: 22px !important; }}
            .content {{ padding: 20px !important; }}
            .info-section {{ padding: 20px !important; margin: 20px 0 !important; }}
            .info-section h3 {{ font-size: 18px !important; }}
            .section-title {{ font-size: 18px !important; }}
            .product-item {{ flex-direction: column; gap: 12px !important; }}
            .product-image-wrapper {{ width: 100%; text-align: center; }}
            .product-price-wrapper {{ text-align: left !important; width: 100%; }}
            .product-price {{ font-size: 16px !important; }}
            .button {{ display: block; width: 100%; margin: 10px 0 !important; padding: 14px 24px !important; }}
            .total-section {{ padding: 20px !important; }}
            .total-label, .total-value {{ font-size: 14px !important; }}
            .total-final-label {{ font-size: 16px !important; }}
            .total-final-value {{ font-size: 20px !important; }}
        }}
    </style>
</head>
<body style='margin: 0; padding: 0; background-color: #f5f7fa;'>
    <div class='email-wrapper'>
        <div class='container'>
            <div class='header'>
                <span class='header-icon'>✅</span>
                <h1>Xác nhận đơn hàng</h1>
                <p style='margin: 15px 0 0 0; font-size: 16px; opacity: 0.95;'>Mã đơn hàng: <strong>#{order.OrderNumber}</strong></p>
            </div>
            
            <div class='content'>
                <div class='greeting'>
                    <p style='margin: 0 0 10px 0;'>Xin chào <strong>{order.GuestFullName ?? order.ShippingFullName}</strong>,</p>
                    <p style='margin: 0; color: #666;'>Cảm ơn bạn đã đặt hàng tại <strong style='color: #4CAF50;'>IKA PickleBall Shop</strong>! Đơn hàng của bạn đã được ghi nhận và chúng tôi đang xử lý.</p>
                </div>
                
                <div class='cta-section'>
                    <a href='{orderDetailUrl}' class='button'>Xem chi tiết đơn hàng →</a>
                </div>
                
                <div class='info-section'>
                    <h3>📋 Thông tin đơn hàng</h3>
                    <div class='info-grid'>
                        <div class='info-row'>
                            <span class='info-label'>Mã đơn hàng</span>
                            <span class='info-value'><strong>#{order.OrderNumber}</strong></span>
                        </div>
                        <div class='info-row'>
                            <span class='info-label'>Ngày đặt hàng</span>
                            <span class='info-value'>{orderDate}</span>
                        </div>
                        <div class='info-row'>
                            <span class='info-label'>Khách hàng</span>
                            <span class='info-value'>{order.GuestFullName ?? order.ShippingFullName}</span>
                        </div>
                        <div class='info-row'>
                            <span class='info-label'>Email</span>
                            <span class='info-value'>{order.GuestEmail}</span>
                        </div>
                        <div class='info-row'>
                            <span class='info-label'>Số điện thoại</span>
                            <span class='info-value'>{order.ShippingPhoneNumber}</span>
                        </div>
                        <div class='info-row'>
                            <span class='info-label'>Địa chỉ giao hàng</span>
                            <span class='info-value'>{shippingAddress}</span>
                        </div>
                        <div class='info-row'>
                            <span class='info-label'>Phương thức thanh toán</span>
                            <span class='info-value'>{paymentMethodText}</span>
                        </div>
                        <div class='info-row'>
                            <span class='info-label'>Trạng thái thanh toán</span>
                            <span class='info-value'><span class='status-badge {paymentStatusClass}'>{paymentStatusText}</span></span>
                        </div>
                        <div class='info-row'>
                            <span class='info-label'>Trạng thái đơn hàng</span>
                            <span class='info-value'><span class='status-badge {orderStatusClass}'>{orderStatusText}</span></span>
                        </div>
                    </div>
                </div>
                
                <div class='order-details-section'>
                    <div class='section-title'>Chi tiết sản phẩm</div>
                    <table class='order-items-table'>
                        <tbody>
                            {orderItemsHtml}
                        </tbody>
                    </table>
                    
                    <div class='total-section'>
                        <div class='total-row'>
                            <span class='total-label'>Tổng giá trị sản phẩm</span>
                            <span class='total-value'>{formatCurrency(order.TotalAmount)}</span>
                        </div>
                        <div class='total-row total-final-row'>
                            <span class='total-label total-final-label'>Tổng cộng</span>
                            <span class='total-value total-final-value'>{formatCurrency(order.TotalAmount)}</span>
                        </div>
                    </div>
                </div>
                
                <div class='support-section'>
                    <h4>💬 Cần hỗ trợ?</h4>
                    <p>Nếu bạn có bất kỳ câu hỏi nào về đơn hàng, vui lòng liên hệ với chúng tôi qua email hoặc số điện thoại hỗ trợ.</p>
                    <p style='margin-top: 15px;'><a href='{continueShoppingUrl}' style='color: #4CAF50; text-decoration: none; font-weight: 600;'>Tiếp tục mua sắm →</a></p>
                </div>
            </div>
            
            <div class='footer'>
                <p style='margin: 0 0 10px 0;'><strong>IKA PickleBall Shop</strong></p>
                <p style='margin: 0;'>© {DateTime.UtcNow.Year} IKA PickleBall Shop. Tất cả quyền được bảo lưu.</p>
            </div>
        </div>
    </div>
</body>
</html>";

        var recipientEmail = order.GuestEmail ?? "";
        
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            _logger.LogError("Cannot send guest order confirmation email: Order {OrderId} has no guest email", order.Id);
            throw new InvalidOperationException($"Cannot send guest order confirmation email: Guest email is required for order {order.Id}");
        }

        _logger.LogInformation("Preparing to send guest order confirmation email to: {Email} for order {OrderId}", recipientEmail, order.Id);

        try
        {
            await SendEmailAsync(recipientEmail, subject, body);
            _logger.LogInformation("Guest order confirmation email sent successfully to: {Email} for order {OrderId}", recipientEmail, order.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send guest order confirmation email to: {Email} for order {OrderId}. Error: {Error}", 
                recipientEmail, order.Id, ex.Message);
            throw;
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
