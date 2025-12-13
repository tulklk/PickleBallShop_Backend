using System.Text.Json.Serialization;

namespace PickleBall.Domain.Entities;

public class NewsPost : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty; // SEO-friendly URL
    public string Content { get; set; } = string.Empty; // HTML content
    public string? Summary { get; set; } // Short description/excerpt
    public string ThumbnailUrl { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public string Category { get; set; } = string.Empty; // "Tin tức", "Kiến thức", "Đánh giá sản phẩm", etc.
    public string? Tags { get; set; } // Comma-separated tags
    public bool IsPublished { get; set; } = false;
    public DateTime? PublishedAt { get; set; }
    public int ViewCount { get; set; } = 0;

    // Navigation properties
    [JsonIgnore]
    public User Author { get; set; } = null!;
}

