namespace PickleBall.Domain.Entities;

public class ProductComment : BaseEntity
{
    public Guid ProductId { get; set; }
    public Guid UserId { get; set; }
    public Guid? ParentCommentId { get; set; } // Null = parent comment, có value = reply comment
    public string Content { get; set; } = string.Empty;
    public bool IsEdited { get; set; } = false;

    // Navigation properties
    public Product Product { get; set; } = null!;
    public User User { get; set; } = null!;
    public ProductComment? ParentComment { get; set; }
    public ICollection<ProductComment> Replies { get; set; } = new List<ProductComment>();
}

