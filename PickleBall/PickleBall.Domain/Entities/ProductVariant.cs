namespace PickleBall.Domain.Entities;

public class ProductVariant : BaseEntity
{
    public Guid ProductId { get; set; }
    public string? Color { get; set; } // Màu sắc (cho vợt)
    public string? Size { get; set; } // Size (cho giày)
    public string? Spec { get; set; } // Thông số (cho vợt: 13mm, 15mm, etc.)
    public decimal Price { get; set; } // Giá của variant (có thể khác với Product base price)
    public decimal? OriginalPrice { get; set; } // Giá gốc nếu có giảm giá
    public int StockQuantity { get; set; } // Số lượng tồn kho riêng cho variant
    public string SKU { get; set; } = string.Empty; // SKU riêng cho variant
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Product Product { get; set; } = null!;
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}

