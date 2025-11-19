using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;

namespace PickleBall.Controllers.Admin;

[ApiController]
[Route("api/admin/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminOrdersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminOrdersController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Order>>> GetOrders(
        [FromQuery] OrderStatus? status = null,
        [FromQuery] PaymentStatus? paymentStatus = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
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

        if (startDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt <= endDate.Value);
        }

        var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
        return Ok(orders);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Order>> GetOrder(Guid id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.User)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound();
        }

        return order;
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateOrderStatus(Guid id, [FromBody] UpdateOrderStatusRequest request)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null)
        {
            return NotFound();
        }

        order.OrderStatus = request.OrderStatus;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    public class UpdateOrderStatusRequest
    {
        public OrderStatus OrderStatus { get; set; }
    }
}
