namespace PickleBall.Domain.Entities;

public class ProductCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }

    // Navigation properties
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ProductCategory? Parent { get; set; }
    public ICollection<ProductCategory> Children { get; set; } = new List<ProductCategory>();
}
