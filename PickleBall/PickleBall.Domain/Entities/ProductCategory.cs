namespace PickleBall.Domain.Entities;

public class ProductCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentCategoryId { get; set; }

    // Navigation properties
    public ProductCategory? ParentCategory { get; set; }
    public ICollection<ProductCategory> SubCategories { get; set; } = new List<ProductCategory>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
