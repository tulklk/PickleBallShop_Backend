using PickleBall.Domain.Entities;

namespace PickleBall.Application.DTOs.Orders;

public class UpdateOrderStatusRequest
{
    public OrderStatus Status { get; set; }
}
