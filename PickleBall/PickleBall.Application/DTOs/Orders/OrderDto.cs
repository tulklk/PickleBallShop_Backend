namespace PickleBall.Application.DTOs.Orders;

public class OrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid? UserId { get; set; } // Nullable for guest orders
    public decimal TotalAmount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
    public string? PaymentMethod { get; set; }
    
    // Guest checkout fields
    public string? GuestEmail { get; set; }
    public string? GuestFullName { get; set; }
    
    public ShippingAddressDto ShippingAddress { get; set; } = null!;
    public List<OrderItemDto> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class OrderItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; } // Variant đã chọn (nếu có)
    public string ProductName { get; set; } = string.Empty;
    public string? VariantInfo { get; set; } // Thông tin variant hiển thị (ví dụ: "Màu: Đỏ, Thông số: 13mm")
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}
