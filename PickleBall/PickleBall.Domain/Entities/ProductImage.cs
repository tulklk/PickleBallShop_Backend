namespace PickleBall.Domain.Entities;

public class ProductImage : BaseEntity
{
    public Guid ProductId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }

    // Navigation property
    public Product Product { get; set; } = null!;
}
