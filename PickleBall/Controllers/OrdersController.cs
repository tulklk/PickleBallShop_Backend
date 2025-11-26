using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PickleBall.Application.DTOs.Orders;
using PickleBall.Application.DTOs.Payments;
using PickleBall.Application.Interfaces;
using System.Security.Claims;

namespace PickleBall.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
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
}
