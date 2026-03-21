using PickleBall.Application.DTOs.Products;
using PickleBall.Application.DTOs.SupportChat;

namespace PickleBall.Application.Interfaces;

public interface ISupportChatService
{
    Task<SupportMessageDto> SendMessageAsync(Guid senderId, string senderRole, Guid customerId, string content);
    Task<PagedResultDto<SupportConversationDto>> GetAdminConversationsAsync(SupportConversationQuery query);
    Task<SupportConversationMessagesDto> GetConversationMessagesForAdminAsync(Guid customerId, SupportMessageQuery query);
    Task<SupportConversationMessagesDto> GetMyConversationMessagesAsync(Guid customerId, SupportMessageQuery query);
    Task MarkConversationReadAsync(Guid viewerId, string viewerRole, Guid customerId);
}
