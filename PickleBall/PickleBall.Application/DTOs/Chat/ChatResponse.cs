using PickleBall.Application.DTOs.Products;

namespace PickleBall.Application.DTOs.Chat;

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; } = true;
    public string? Error { get; set; }
    public List<ProductSuggestionDto>? SuggestedProducts { get; set; }
}

public class ProductSuggestionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string? PrimaryImageUrl { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public double? AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public int StockQuantity { get; set; }
}

