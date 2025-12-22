using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PickleBall.Application.DTOs.Orders;
using PickleBall.Application.DTOs.Payments;
using PickleBall.Application.Interfaces;
using PickleBall.Infrastructure.Data;
using System.Security.Claims;

namespace PickleBall.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IEmailService _emailService;
    private readonly ApplicationDbContext _context;
    private readonly IVoucherService _voucherService;

    public OrdersController(IOrderService orderService, IEmailService emailService, ApplicationDbContext context, IVoucherService voucherService)
    {
        _orderService = orderService;
        _emailService = emailService;
        _context = context;
        _voucherService = voucherService;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdClaim ?? throw new UnauthorizedAccessException());
    }

    /// <summary>
    /// Áp dụng mã giảm giá cho giỏ hàng của user đã đăng nhập
    /// </summary>
    [HttpPost("apply-voucher")]
    public async Task<ActionResult> ApplyVoucher([FromBody] ApplyVoucherForCartRequest request)
    {
        var userId = GetUserId();

        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null || !cart.Items.Any())
        {
            return BadRequest(new { message = "Giỏ hàng trống" });
        }

        var orderTotal = cart.Items.Sum(ci => ci.UnitPriceSnapshot * ci.Quantity);

        var result = await _voucherService.ApplyForUserAsync(userId, orderTotal, request.Code);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<object>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        try
        {
            var userId = GetUserId();
            var order = await _orderService.CreateOrderAsync(userId, request);
            
            // For COD orders, return order with payment info indicating COD
            if (request.PaymentMethod == Domain.Entities.PaymentMethod.COD)
            {
                return Ok(new
                {
                    Order = order,
                    Payment = new
                    {
                        PaymentUrl = (string?)null,
                        OrderId = order.Id,
                        OrderNumber = order.OrderNumber,
                        IsCod = true,
                        IsBankTransfer = false
                    }
                });
            }
            
            // For Bank Transfer orders, immediately generate QR code and return
            if (request.PaymentMethod == Domain.Entities.PaymentMethod.BankTransfer)
            {
                var paymentResponse = await _orderService.InitiatePaymentAsync(order.Id, userId);
                return Ok(new
                {
                    Order = order,
                    Payment = paymentResponse
                });
            }
            
            return Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("my")]
    public async Task<ActionResult<List<OrderDto>>> GetMyOrders()
    {
        var userId = GetUserId();
        var orders = await _orderService.GetUserOrdersAsync(userId);
        return Ok(orders);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> GetOrder(Guid id)
    {
        var userId = GetUserId();
        var order = await _orderService.GetOrderByIdAsync(id, userId);
        if (order == null)
        {
            return NotFound();
        }
        return Ok(order);
    }

    /// <summary>
    /// Get order status by ID (lightweight - only returns status info)
    /// </summary>
    [HttpGet("{id:guid}/status")]
    public async Task<ActionResult<OrderStatusDto>> GetOrderStatus(Guid id)
    {
        var userId = GetUserId();
        
        var order = await _context.Orders
            .Where(o => o.Id == id && o.UserId == userId)
            .Select(o => new OrderStatusDto
            {
                OrderId = o.Id,
                OrderNumber = o.OrderNumber,
                OrderStatus = o.OrderStatus.ToString(),
                PaymentStatus = o.PaymentStatus.ToString(),
                PaymentMethod = o.PaymentMethod.ToString(),
                TotalAmount = o.TotalAmount,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt,
                StatusDescription = GetStatusDescription(o.OrderStatus, o.PaymentStatus)
            })
            .FirstOrDefaultAsync();

        if (order == null)
        {
            return NotFound(new { message = "Không tìm thấy đơn hàng" });
        }

        return Ok(order);
    }

    /// <summary>
    /// Get all orders status summary for current user
    /// </summary>
    [HttpGet("my/status")]
    public async Task<ActionResult<UserOrdersSummaryDto>> GetMyOrdersStatus()
    {
        var userId = GetUserId();
        
        var orders = await _context.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderStatusDto
            {
                OrderId = o.Id,
                OrderNumber = o.OrderNumber,
                OrderStatus = o.OrderStatus.ToString(),
                PaymentStatus = o.PaymentStatus.ToString(),
                PaymentMethod = o.PaymentMethod.ToString(),
                TotalAmount = o.TotalAmount,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt,
                StatusDescription = GetStatusDescription(o.OrderStatus, o.PaymentStatus)
            })
            .ToListAsync();

        var summary = new UserOrdersSummaryDto
        {
            TotalOrders = orders.Count,
            ProcessingOrders = orders.Count(o => o.OrderStatus == "Processing"),
            ConfirmedOrders = orders.Count(o => o.OrderStatus == "Confirmed"),
            PreparedOrders = orders.Count(o => o.OrderStatus == "Prepared"),
            ShippedOrders = orders.Count(o => o.OrderStatus == "Shipped"),
            CompletedOrders = orders.Count(o => o.OrderStatus == "Completed"),
            CancelledOrders = orders.Count(o => o.OrderStatus == "Cancelled"),
            Orders = orders
        };

        return Ok(summary);
    }

    /// <summary>
    /// Helper method to get Vietnamese status description
    /// </summary>
    private static string GetStatusDescription(Domain.Entities.OrderStatus orderStatus, Domain.Entities.PaymentStatus paymentStatus)
    {
        var paymentDesc = paymentStatus switch
        {
            Domain.Entities.PaymentStatus.Pending => "Chờ thanh toán",
            Domain.Entities.PaymentStatus.Paid => "Đã thanh toán",
            Domain.Entities.PaymentStatus.Failed => "Thanh toán thất bại",
            Domain.Entities.PaymentStatus.Refunded => "Đã hoàn tiền",
            _ => "Không xác định"
        };

        var orderDesc = orderStatus switch
        {
            Domain.Entities.OrderStatus.Processing => "Đang xử lý",
            Domain.Entities.OrderStatus.Confirmed => "Đã xác nhận",
            Domain.Entities.OrderStatus.Prepared => "Đã chuẩn bị hàng",
            Domain.Entities.OrderStatus.Shipped => "Đã giao cho đơn vị vận chuyển",
            Domain.Entities.OrderStatus.Completed => "Hoàn thành",
            Domain.Entities.OrderStatus.Cancelled => "Đã hủy",
            _ => "Không xác định"
        };

        return $"{orderDesc} - {paymentDesc}";
    }

    [HttpPost("{id:guid}/payment")]
    public async Task<ActionResult<CreatePaymentResponse>> InitiatePayment(Guid id)
    {
        try
        {
            var userId = GetUserId();
            var paymentResponse = await _orderService.InitiatePaymentAsync(id, userId);
            if (paymentResponse == null)
            {
                return BadRequest(new { message = "Failed to initiate payment" });
            }
            return Ok(paymentResponse);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Test endpoint to send order confirmation email
    /// </summary>
    [HttpPost("{id:guid}/test-email")]
    public async Task<ActionResult> TestOrderEmail(Guid id)
    {
        try
        {
            var userId = GetUserId();
            
            // Load order with all navigation properties needed for email
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Items)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Images)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                return NotFound(new { message = "Order not found" });
            }

            // Validate user email
            if (order.User == null || string.IsNullOrWhiteSpace(order.User.Email))
            {
                return BadRequest(new 
                { 
                    message = "Cannot send order confirmation email: User email not found",
                    orderId = order.Id,
                    userId = order.UserId
                });
            }

            // Get client IP address
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

            // Send order confirmation email
            await _emailService.SendOrderConfirmationEmailAsync(order, clientIp);

            return Ok(new 
            { 
                message = "Order confirmation email sent successfully",
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                recipientEmail = order.User.Email,
                recipientName = order.User.FullName,
                totalAmount = order.TotalAmount,
                paymentStatus = order.PaymentStatus.ToString(),
                orderStatus = order.OrderStatus.ToString(),
                note = "This is a test email for order confirmation"
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new 
            { 
                message = "Failed to send order confirmation email",
                error = ex.Message,
                details = ex.InnerException?.Message
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new 
            { 
                message = "Failed to send order confirmation email",
                error = ex.Message,
                details = ex.InnerException?.Message,
                type = ex.GetType().Name
            });
        }
    }
}
