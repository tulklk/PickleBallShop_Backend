namespace PickleBall.Domain.Entities;

public class ProductReview : BaseEntity
{
    public Guid ProductId { get; set; }
    public Guid UserId { get; set; }
    public int Rating { get; set; } // 1-5
    public string? Comment { get; set; }

    // Navigation properties
    public Product Product { get; set; } = null!;
    public User User { get; set; } = null!;
}
