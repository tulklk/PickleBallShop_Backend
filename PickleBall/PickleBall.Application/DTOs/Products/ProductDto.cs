namespace PickleBall.Application.DTOs.Products;

public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public int StockQuantity { get; set; }
    public string? Brand { get; set; }
    public bool IsActive { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public string? PrimaryImageUrl { get; set; }
    public double? AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public bool HasVariants { get; set; }
    public List<ProductVariantDto> Variants { get; set; } = new();
    public List<ProductSpecificationDto> Specifications { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}
