namespace PickleBall.Domain.Entities;

public class SupportMessage : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsFromAdmin { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    // Navigation properties
    public SupportConversation Conversation { get; set; } = null!;
    public User Sender { get; set; } = null!;
}
