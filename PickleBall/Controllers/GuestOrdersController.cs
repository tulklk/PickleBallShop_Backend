using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PickleBall.Application.DTOs.Orders;
using PickleBall.Application.DTOs.Payments;
using PickleBall.Application.Interfaces;
using PickleBall.Infrastructure.Data;

namespace PickleBall.Controllers;

/// <summary>
/// Guest checkout - allows users to purchase without logging in.
/// Every read requires the order number (or id) AND the email used at checkout; email alone never
/// grants access to any order data.
/// </summary>
[ApiController]
[Route("api/guest/orders")]
public class GuestOrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<GuestOrdersController> _logger;
    private readonly IVoucherService _voucherService;

    public GuestOrdersController(
        IOrderService orderService,
        ApplicationDbContext context,
        ILogger<GuestOrdersController> logger,
        IVoucherService voucherService)
    {
        _orderService = orderService;
        _context = context;
        _logger = logger;
        _voucherService = voucherService;
    }

    private static readonly object NotFoundBody = new { message = "Không tìm thấy đơn hàng" };

    /// <summary>
    /// Áp dụng mã giảm giá cho đơn khách vãng lai (chưa tạo đơn). The subtotal is computed by the server.
    /// </summary>
    [EnableRateLimiting("voucher")]
    [HttpPost("apply-voucher")]
    public async Task<ActionResult> ApplyVoucherForGuest([FromBody] ApplyVoucherForGuestOrderRequest request)
    {
        try
        {
            var totalAmount = await _orderService.CalculateGuestSubtotalAsync(request.Items);
            var result = await _voucherService.ApplyForGuestAsync(request.Email, totalAmount, request.Code);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Create a guest order without requiring authentication
    /// </summary>
    [EnableRateLimiting("guest-order")]
    [HttpPost]
    public async Task<ActionResult<object>> CreateGuestOrder([FromBody] CreateGuestOrderRequest request)
    {
        try
        {
            var order = await _orderService.CreateGuestOrderAsync(request);

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
                var paymentResponse = await _orderService.InitiateGuestPaymentAsync(order.Id, request.Email);
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
            _logger.LogWarning("Invalid guest order request: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating guest order");
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi tạo đơn hàng. Vui lòng thử lại sau." });
        }
    }

    /// <summary>
    /// Get guest order by ID and email (for verification)
    /// </summary>
    [EnableRateLimiting("guest-lookup")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> GetGuestOrder(Guid id, [FromQuery] string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Email là bắt buộc để xem đơn hàng" });
        }

        var order = await _orderService.GetGuestOrderByIdAsync(id, email);
        if (order == null)
        {
            return NotFound(NotFoundBody);
        }

        return Ok(order);
    }

    /// <summary>
    /// Look up ONE guest order. Requires both the order number and the email used at checkout.
    /// (Listing every order of an email address was removed: it disclosed other people's personal data.)
    /// </summary>
    [EnableRateLimiting("guest-lookup")]
    [HttpGet("lookup")]
    public async Task<ActionResult<List<OrderDto>>> LookupGuestOrders(
        [FromQuery] string? email, [FromQuery] string? orderNumber)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(orderNumber))
        {
            return BadRequest(new { message = "Cần nhập cả mã đơn hàng và email để tra cứu" });
        }

        var order = await _orderService.GetGuestOrderAsync(orderNumber, email);
        if (order == null)
        {
            return NotFound(NotFoundBody);
        }

        return Ok(new List<OrderDto> { order });
    }

    /// <summary>
    /// Initiate payment for guest order
    /// </summary>
    [EnableRateLimiting("guest-lookup")]
    [HttpPost("{id:guid}/payment")]
    public async Task<ActionResult<CreatePaymentResponse>> InitiateGuestPayment(Guid id, [FromQuery] string? email)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new { message = "Email là bắt buộc" });
            }

            var paymentResponse = await _orderService.InitiateGuestPaymentAsync(id, email);
            if (paymentResponse == null)
            {
                return BadRequest(new { message = "Không thể khởi tạo thanh toán" });
            }

            return Ok(paymentResponse);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(NotFoundBody);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get order status by order number. The email used at checkout must match, otherwise the order
    /// number alone (which can leak through screenshots, emails, etc.) would disclose order data.
    /// </summary>
    [EnableRateLimiting("guest-lookup")]
    [HttpGet("status/{orderNumber}")]
    public async Task<ActionResult<OrderStatusDto>> GetGuestOrderStatusByOrderNumber(
        string orderNumber, [FromQuery] string? email)
    {
        if (string.IsNullOrWhiteSpace(orderNumber) || string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Mã đơn hàng và email là bắt buộc" });
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var number = orderNumber.Trim();

        var order = await _context.Orders
            .AsNoTracking()
            .Where(o => o.OrderNumber == number && o.UserId == null &&
                        o.GuestEmail != null && o.GuestEmail.ToLower() == normalizedEmail)
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
            return NotFound(NotFoundBody);
        }

        return Ok(order);
    }

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
}
