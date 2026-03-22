namespace PickleBall.Application.DTOs.Notifications;

/// <summary>
/// Payload pushed to admin clients when a new order is created.
/// </summary>
public class NewOrderNotificationDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsGuestOrder { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerName { get; set; }
    public string? PaymentMethod { get; set; }
}
