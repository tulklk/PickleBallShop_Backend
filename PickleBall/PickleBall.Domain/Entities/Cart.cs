namespace PickleBall.Domain.Entities;

public class Cart : BaseEntity
{
    public Guid? UserId { get; set; }

    // Navigation properties
    public User? User { get; set; }
    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}
