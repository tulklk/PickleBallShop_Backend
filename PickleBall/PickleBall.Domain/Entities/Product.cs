using System.Text.Json.Serialization;

namespace PickleBall.Domain.Entities;

public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public int StockQuantity { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    [JsonIgnore]
    public ProductCategory Category { get; set; } = null!;
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<ProductReview> Reviews { get; set; } = new List<ProductReview>();
    public ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
    public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
}
