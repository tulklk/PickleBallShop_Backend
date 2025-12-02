namespace PickleBall.Domain.Entities;

public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; } // Variant đã chọn (nếu có)
    public string ProductNameSnapshot { get; set; } = string.Empty;
    public string? VariantInfoSnapshot { get; set; } // Lưu thông tin variant như snapshot (ví dụ: "Màu: Đỏ, Size: 42")
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }

    // Navigation properties
    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public ProductVariant? ProductVariant { get; set; }
}
