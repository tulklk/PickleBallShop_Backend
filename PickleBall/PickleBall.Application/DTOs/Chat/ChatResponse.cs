namespace PickleBall.Application.DTOs.Chat;

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; } = true;
    public string? Error { get; set; }
}

