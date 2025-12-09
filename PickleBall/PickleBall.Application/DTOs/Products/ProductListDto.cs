namespace PickleBall.Application.DTOs.Products;

public class ProductListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public int StockQuantity { get; set; }
    public string? Brand { get; set; }
    public string? PrimaryImageUrl { get; set; }
    public double? AverageRating { get; set; }
    public int ReviewCount { get; set; }
}
