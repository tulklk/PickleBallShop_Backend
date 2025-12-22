namespace PickleBall.Application.DTOs.Orders;

/// <summary>
/// Lightweight DTO for order status checking
/// </summary>
public class OrderStatusDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string? PaymentMethod { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    /// <summary>
    /// Human-readable status description in Vietnamese
    /// </summary>
    public string StatusDescription { get; set; } = string.Empty;
}

/// <summary>
/// Summary of all user's orders status
/// </summary>
public class UserOrdersSummaryDto
{
    public int TotalOrders { get; set; }
    public int ProcessingOrders { get; set; }
    public int ConfirmedOrders { get; set; }
    public int PreparedOrders { get; set; }
    public int ShippedOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public List<OrderStatusDto> Orders { get; set; } = new();
}

