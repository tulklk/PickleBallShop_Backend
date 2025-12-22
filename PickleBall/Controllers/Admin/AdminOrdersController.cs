using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PickleBall.Application.DTOs.Orders;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;

namespace PickleBall.Controllers.Admin;

[ApiController]
[Route("api/admin/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminOrdersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IOrderService _orderService;

    public AdminOrdersController(ApplicationDbContext context, IMapper mapper, IOrderService orderService)
    {
        _context = context;
        _mapper = mapper;
        _orderService = orderService;
    }

    /// <summary>
    /// Get all orders with filters (Admin only)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<AdminOrderDto>>> GetOrders(
        [FromQuery] OrderStatus? status = null,
        [FromQuery] PaymentStatus? paymentStatus = null,
        [FromQuery] PaymentMethod? paymentMethod = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _context.Orders
            .Include(o => o.Items)
            .Include(o => o.User)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(o => o.OrderStatus == status.Value);
        }

        if (paymentStatus.HasValue)
        {
            query = query.Where(o => o.PaymentStatus == paymentStatus.Value);
        }

        if (paymentMethod.HasValue)
        {
            query = query.Where(o => o.PaymentMethod == paymentMethod.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt <= endDate.Value);
        }

        var totalCount = await query.CountAsync();

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var orderDtos = _mapper.Map<List<AdminOrderDto>>(orders);

        Response.Headers.Append("X-Total-Count", totalCount.ToString());
        Response.Headers.Append("X-Page", page.ToString());
        Response.Headers.Append("X-Page-Size", pageSize.ToString());
        Response.Headers.Append("X-Total-Pages", ((int)Math.Ceiling(totalCount / (double)pageSize)).ToString());

        return Ok(orderDtos);
    }

    /// <summary>
    /// Get order by ID (Admin only)
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminOrderDto>> GetOrder(Guid id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound(new { message = "Order not found" });
        }

        var orderDto = _mapper.Map<AdminOrderDto>(order);
        return Ok(orderDto);
    }

    /// <summary>
    /// Cập nhật trạng thái đơn hàng (Admin only)
    /// </summary>
    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<AdminOrderDto>> UpdateOrderStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request)
    {
        try
        {
            await _orderService.UpdateOrderStatusAsync(id, request.Status);
            
            var order = await _context.Orders
                .Include(o => o.Items)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound(new { message = "Không tìm thấy đơn hàng" });
            }

            var adminOrderDto = _mapper.Map<AdminOrderDto>(order);
            return Ok(adminOrderDto);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // NOTE: PaymentStatus can ONLY be updated by payment gateway callbacks (MoMo, VNPay, PayOS)
    // - MoMo: /api/payments/momo/ipn and /api/payments/momo/return
    // - VNPay: /api/payments/vnpay/ipn and /api/payments/vnpay/return  
    // - PayOS: /api/payments/payos/webhook and /api/payments/payos/confirm
    // - COD: PaymentStatus remains Pending (payment collected on delivery)
    // Admin không thể cập nhật PaymentStatus thủ công để đảm bảo tính toàn vẹn dữ liệu
}
