namespace PickleBall.Application.DTOs.Orders;

public class AdminOrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid? UserId { get; set; } // Nullable for guest orders
    public UserInfoDto? User { get; set; }
    
    // Guest checkout fields (used when UserId is null)
    public string? GuestEmail { get; set; }
    public string? GuestFullName { get; set; }
    public bool IsGuestOrder => UserId == null;
    
    public decimal TotalAmount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
    public string? PaymentMethod { get; set; }
    public string? PaymentTransactionId { get; set; }
    public ShippingAddressDto ShippingAddress { get; set; } = null!;
    public List<OrderItemDto> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UserInfoDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
}

