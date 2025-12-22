using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PickleBall.Application.DTOs.Orders;
using PickleBall.Application.DTOs.Payments;
using PickleBall.Application.Interfaces;
using PickleBall.Infrastructure.Data;

namespace PickleBall.Controllers;

/// <summary>
/// Controller for guest checkout - allows users to purchase without logging in
/// </summary>
[ApiController]
[Route("api/guest/orders")]
public class GuestOrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IEmailService _emailService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<GuestOrdersController> _logger;
    private readonly IMapper _mapper;
    private readonly IVoucherService _voucherService;

    public GuestOrdersController(
        IOrderService orderService, 
        IEmailService emailService, 
        ApplicationDbContext context,
        ILogger<GuestOrdersController> logger,
        IMapper mapper,
        IVoucherService voucherService)
    {
        _orderService = orderService;
        _emailService = emailService;
        _context = context;
        _logger = logger;
        _mapper = mapper;
        _voucherService = voucherService;
    }

    /// <summary>
    /// Áp dụng mã giảm giá cho đơn khách vãng lai (chưa tạo đơn)
    /// </summary>
    [HttpPost("apply-voucher")]
    public async Task<ActionResult> ApplyVoucherForGuest([FromBody] ApplyVoucherForGuestOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Email là bắt buộc" });
        }

        if (request.Items == null || !request.Items.Any())
        {
            return BadRequest(new { message = "Giỏ hàng không được trống" });
        }

        // Tính tạm tổng giống CreateGuestOrderAsync
        var productIds = request.Items.Select(i => i.ProductId).ToList();
        var variantIds = request.Items.Where(i => i.ProductVariantId.HasValue).Select(i => i.ProductVariantId!.Value).ToList();

        var products = await _context.Products
            .Include(p => p.Variants)
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();

        var variants = variantIds.Any()
            ? await _context.ProductVariants.Where(v => variantIds.Contains(v.Id)).ToListAsync()
            : new List<Domain.Entities.ProductVariant>();

        decimal totalAmount = 0;

        foreach (var item in request.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product == null)
            {
                return BadRequest(new { message = $"Không tìm thấy sản phẩm với ID {item.ProductId}" });
            }

            decimal price;
            if (product.HasVariants && item.ProductVariantId.HasValue)
            {
                var variant = variants.FirstOrDefault(v => v.Id == item.ProductVariantId);
                if (variant == null)
                {
                    return BadRequest(new { message = $"Không tìm thấy biến thể sản phẩm với ID {item.ProductVariantId}" });
                }
                price = variant.Price;
            }
            else
            {
                price = product.Price;
            }

            totalAmount += price * item.Quantity;
        }

        var result = await _voucherService.ApplyForGuestAsync(request.Email, totalAmount, request.Code);
        return Ok(result);
    }

    /// <summary>
    /// Create a guest order without requiring authentication
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<object>> CreateGuestOrder([FromBody] CreateGuestOrderRequest request)
    {
        try
        {
            // Validate request
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { message = "Email là bắt buộc" });
            }

            if (string.IsNullOrWhiteSpace(request.FullName))
            {
                return BadRequest(new { message = "Họ tên là bắt buộc" });
            }

            if (request.Items == null || !request.Items.Any())
            {
                return BadRequest(new { message = "Giỏ hàng không được trống" });
            }

            if (request.ShippingAddress == null)
            {
                return BadRequest(new { message = "Địa chỉ giao hàng là bắt buộc" });
            }

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
            _logger.LogWarning(ex, "Invalid guest order request: {Message}", ex.Message);
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
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> GetGuestOrder(Guid id, [FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Email là bắt buộc để xem đơn hàng" });
        }

        var order = await _orderService.GetGuestOrderByIdAsync(id, email);
        if (order == null)
        {
            return NotFound(new { message = "Không tìm thấy đơn hàng" });
        }
        return Ok(order);
    }

    /// <summary>
    /// Look up guest orders by email
    /// </summary>
    [HttpGet("lookup")]
    public async Task<ActionResult<List<OrderDto>>> LookupGuestOrders([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Email là bắt buộc để tra cứu đơn hàng" });
        }

        var orders = await _context.Orders
            .Include(o => o.Items)
            .Where(o => o.GuestEmail == email && o.UserId == null)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return Ok(_mapper.Map<List<OrderDto>>(orders));
    }

    /// <summary>
    /// Initiate payment for guest order
    /// </summary>
    [HttpPost("{id:guid}/payment")]
    public async Task<ActionResult<CreatePaymentResponse>> InitiateGuestPayment(Guid id, [FromQuery] string email)
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
    /// Get order status by order number (no login required)
    /// Guest can check order status using the order number received after checkout
    /// </summary>
    [HttpGet("status/{orderNumber}")]
    public async Task<ActionResult<OrderStatusDto>> GetGuestOrderStatusByOrderNumber(string orderNumber)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return BadRequest(new { message = "Mã đơn hàng là bắt buộc" });
        }

        var order = await _context.Orders
            .Where(o => o.OrderNumber == orderNumber)
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
            return NotFound(new { message = "Không tìm thấy đơn hàng với mã này" });
        }

        return Ok(order);
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
}

