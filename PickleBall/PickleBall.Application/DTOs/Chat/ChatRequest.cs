namespace PickleBall.Application.DTOs.Chat;

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public List<ChatMessage>? History { get; set; } // Optional conversation history
}

public class ChatMessage
{
    public string Role { get; set; } = string.Empty; // "user" or "model"
    public string Content { get; set; } = string.Empty;
}

