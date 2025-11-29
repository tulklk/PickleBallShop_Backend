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

    public OrdersController(IOrderService orderService, IEmailService emailService, ApplicationDbContext context)
    {
        _orderService = orderService;
        _emailService = emailService;
        _context = context;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdClaim ?? throw new UnauthorizedAccessException());
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
