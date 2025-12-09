using System.Text.Json.Serialization;

namespace PickleBall.Domain.Entities;

public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty; // SEO-friendly URL slug
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public int StockQuantity { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public bool IsActive { get; set; } = true;
    public bool HasVariants { get; set; } = false; // Có variants hay không (vợt, giày có variants)

    // Navigation properties
    [JsonIgnore]
    public ProductCategory Category { get; set; } = null!;
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<ProductReview> Reviews { get; set; } = new List<ProductReview>();
    public ICollection<ProductComment> Comments { get; set; } = new List<ProductComment>();
    public ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
    public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
}
