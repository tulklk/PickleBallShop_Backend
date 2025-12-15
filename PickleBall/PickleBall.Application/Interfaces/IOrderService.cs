using PickleBall.Application.DTOs.Orders;
using PickleBall.Application.DTOs.Payments;

namespace PickleBall.Application.Interfaces;

public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(Guid userId, CreateOrderRequest request);
    Task<OrderDto> CreateGuestOrderAsync(CreateGuestOrderRequest request);
    Task<List<OrderDto>> GetUserOrdersAsync(Guid userId);
    Task<OrderDto?> GetOrderByIdAsync(Guid orderId, Guid userId);
    Task<OrderDto?> GetGuestOrderByIdAsync(Guid orderId, string email);
    Task<CreatePaymentResponse?> InitiatePaymentAsync(Guid orderId, Guid userId);
    Task<CreatePaymentResponse?> InitiateGuestPaymentAsync(Guid orderId, string email);
    Task UpdateOrderPaymentStatusAsync(Guid orderId, string transactionId, bool isPaid, string? clientIpAddress = null);
}
