using PickleBall.Domain.Entities;

namespace PickleBall.Application.DTOs.Products;

public class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public int StockQuantity { get; set; } = 0;
    public string SKU { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Pickleball-specific fields
    public ProductType ProductType { get; set; }
    public SkillLevel? SkillLevel { get; set; }
    public WeightRange? WeightRange { get; set; }
    public SurfaceType? SurfaceType { get; set; }
    public string? Material { get; set; }
    
    // Images
    public List<string> ImageUrls { get; set; } = new();
}

