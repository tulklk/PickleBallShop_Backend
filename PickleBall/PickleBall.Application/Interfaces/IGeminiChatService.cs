using PickleBall.Application.DTOs.Chat;

namespace PickleBall.Application.Interfaces;

public interface IGeminiChatService
{
    Task<ChatResponse> SendMessageAsync(ChatRequest request);
}

