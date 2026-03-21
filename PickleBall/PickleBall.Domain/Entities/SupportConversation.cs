namespace PickleBall.Domain.Entities;

public class SupportConversation : BaseEntity
{
    public Guid CustomerId { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public string? LastMessagePreview { get; set; }
    public int CustomerUnreadCount { get; set; }
    public int AdminUnreadCount { get; set; }

    // Navigation properties
    public User Customer { get; set; } = null!;
    public ICollection<SupportMessage> Messages { get; set; } = new List<SupportMessage>();
}
