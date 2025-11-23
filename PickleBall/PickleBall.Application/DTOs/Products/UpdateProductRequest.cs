using System.Text.Json.Serialization;
using PickleBall.Domain.Entities;

namespace PickleBall.Application.DTOs.Products;

public class UpdateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    
    [JsonIgnore]
    public ProductCategory? Category { get; set; }
    
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public int StockQuantity { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Images
    public List<string> ImageUrls { get; set; } = new();
}

