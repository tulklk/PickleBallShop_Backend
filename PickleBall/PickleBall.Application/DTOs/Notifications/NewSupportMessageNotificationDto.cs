namespace PickleBall.Application.DTOs.Notifications;

/// <summary>
/// Payload pushed to admin clients when a customer sends a new support message.
/// </summary>
public class NewSupportMessageNotificationDto
{
    public Guid MessageId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    /// <summary>Short preview for list/toast (same as conversation last preview).</summary>
    public string? ContentPreview { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>Total unread for this conversation from customer side (for admin badge).</summary>
    public int AdminUnreadCount { get; set; }
}
