namespace PickleBall.Application.DTOs.SupportChat;

public class SendSupportMessageRequest
{
    public string Content { get; set; } = string.Empty;
}

public class SupportMessageDto
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public bool IsFromAdmin { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SupportConversationDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public DateTime? LastMessageAt { get; set; }
    public string? LastMessagePreview { get; set; }
    public int CustomerUnreadCount { get; set; }
    public int AdminUnreadCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SupportConversationMessagesDto
{
    public SupportConversationDto Conversation { get; set; } = new();
    public List<SupportMessageDto> Messages { get; set; } = new();
}

public class SupportConversationQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class SupportMessageQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
