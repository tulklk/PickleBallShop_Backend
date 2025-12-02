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
    
    // Images
    public List<string> ImageUrls { get; set; } = new();
    
    // Variants (optional - nếu có variants thì product.HasVariants = true)
    public List<CreateProductVariantRequest>? Variants { get; set; }
}

