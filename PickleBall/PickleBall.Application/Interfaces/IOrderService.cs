using PickleBall.Application.DTOs.Orders;
using PickleBall.Application.DTOs.Payments;
using PickleBall.Domain.Entities;

namespace PickleBall.Application.Interfaces;

public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(Guid userId, CreateOrderRequest request);
    Task<OrderDto> CreateGuestOrderAsync(CreateGuestOrderRequest request);
    Task<List<OrderDto>> GetUserOrdersAsync(Guid userId);
    Task<OrderDto?> GetOrderByIdAsync(Guid orderId, Guid userId);

    /// <summary>Server-side subtotal (current catalog prices) of client-supplied guest cart lines.</summary>
    Task<decimal> CalculateGuestSubtotalAsync(IEnumerable<GuestCartItemDto> items);

    /// <summary>Server-side subtotal (current catalog prices) of the user's cart.</summary>
    Task<decimal> CalculateCartSubtotalAsync(Guid userId);

    /// <summary>Returns the guest order only if order number and email both match.</summary>
    Task<OrderDto?> GetGuestOrderAsync(string orderNumber, string email);
    Task<OrderDto?> GetGuestOrderByIdAsync(Guid orderId, string email);
    Task<CreatePaymentResponse?> InitiatePaymentAsync(Guid orderId, Guid userId);
    Task<CreatePaymentResponse?> InitiateGuestPaymentAsync(Guid orderId, string email);

    /// <summary>
    /// Marks an order as paid after the payment was VERIFIED server-side (gateway signature or gateway API).
    /// Idempotent and monotonic: an order that is already Paid is never changed again.
    /// Returns true only when this call changed the order to Paid.
    /// </summary>
    Task<bool> ConfirmPaymentAsync(
        Guid orderId,
        PaymentMethod gateway,
        string transactionId,
        decimal paidAmount,
        string currency,
        string? rawResponse,
        string? clientIpAddress = null);

    /// <summary>Marks a still-pending payment as failed. Never downgrades a Paid order.</summary>
    Task<bool> MarkPaymentFailedAsync(Guid orderId, string transactionId);

    Task<OrderDto> UpdateOrderStatusAsync(Guid orderId, OrderStatus status);
}
