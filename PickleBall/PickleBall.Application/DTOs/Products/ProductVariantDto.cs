namespace PickleBall.Application.DTOs.Products;

public class ProductVariantDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string? Color { get; set; }
    public string? Size { get; set; }
    public string? Spec { get; set; }
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public int StockQuantity { get; set; }
    public string SKU { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    
    // Helper property để hiển thị thông tin variant
    public string DisplayName
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Color))
                parts.Add($"Màu: {Color}");
            if (!string.IsNullOrWhiteSpace(Size))
                parts.Add($"Size: {Size}");
            if (!string.IsNullOrWhiteSpace(Spec))
                parts.Add($"Thông số: {Spec}");
            return parts.Any() ? string.Join(", ", parts) : "Default";
        }
    }
}

