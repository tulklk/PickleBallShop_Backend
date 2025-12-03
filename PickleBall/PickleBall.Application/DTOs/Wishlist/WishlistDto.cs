namespace PickleBall.Application.DTOs.Wishlist;

public class WishlistDto
{
    public List<WishlistItemDto> Items { get; set; } = new();
    public int TotalItems { get; set; }
}

public class WishlistItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductDescription { get; set; }
    public string? ProductImageUrl { get; set; }
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public bool IsActive { get; set; }
    public bool HasVariants { get; set; }
    public DateTime AddedAt { get; set; }
}

