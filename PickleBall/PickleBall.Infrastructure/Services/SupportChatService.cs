using Microsoft.EntityFrameworkCore;
using PickleBall.Application.DTOs.Products;
using PickleBall.Application.DTOs.SupportChat;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;

namespace PickleBall.Infrastructure.Services;

public class SupportChatService : ISupportChatService
{
    private readonly ApplicationDbContext _context;

    public SupportChatService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SupportMessageDto> SendMessageAsync(Guid senderId, string senderRole, Guid customerId, string content)
    {
        var normalizedRole = senderRole?.Trim() ?? string.Empty;
        var isAdmin = normalizedRole.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        var isCustomer = normalizedRole.Equals("Customer", StringComparison.OrdinalIgnoreCase);

        if (!isAdmin && !isCustomer)
        {
            throw new UnauthorizedAccessException("Only Admin or Customer can send support messages");
        }

        if (isCustomer && senderId != customerId)
        {
            throw new UnauthorizedAccessException("Customer can only send messages to their own conversation");
        }

        var sender = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == senderId && u.IsActive);
        if (sender == null)
        {
            throw new KeyNotFoundException("Sender not found");
        }

        var customer = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == customerId && u.IsActive);
        if (customer == null)
        {
            throw new KeyNotFoundException("Customer not found");
        }

        var trimmedContent = content?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedContent))
        {
            throw new ArgumentException("Message content is required");
        }

        var conversation = await _context.SupportConversations
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (conversation == null)
        {
            conversation = new SupportConversation
            {
                CustomerId = customerId,
                LastMessageAt = DateTime.UtcNow
            };
            _context.SupportConversations.Add(conversation);
            await _context.SaveChangesAsync();
        }

        var message = new SupportMessage
        {
            ConversationId = conversation.Id,
            SenderId = senderId,
            Content = trimmedContent,
            IsFromAdmin = isAdmin,
            IsRead = false
        };

        _context.SupportMessages.Add(message);

        conversation.LastMessageAt = DateTime.UtcNow;
        conversation.LastMessagePreview = trimmedContent.Length > 500
            ? trimmedContent[..500]
            : trimmedContent;
        if (isAdmin)
        {
            conversation.CustomerUnreadCount += 1;
        }
        else
        {
            conversation.AdminUnreadCount += 1;
        }

        await _context.SaveChangesAsync();

        return new SupportMessageDto
        {
            Id = message.Id,
            ConversationId = conversation.Id,
            SenderId = sender.Id,
            SenderName = sender.FullName,
            IsFromAdmin = message.IsFromAdmin,
            Content = message.Content,
            IsRead = message.IsRead,
            ReadAt = message.ReadAt,
            CreatedAt = message.CreatedAt
        };
    }

    public async Task<PagedResultDto<SupportConversationDto>> GetAdminConversationsAsync(SupportConversationQuery query)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var conversationsQuery = _context.SupportConversations
            .AsNoTracking()
            .Include(c => c.Customer)
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt);

        var totalCount = await conversationsQuery.CountAsync();
        var items = await conversationsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => MapConversation(c))
            .ToListAsync();

        return new PagedResultDto<SupportConversationDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<SupportConversationMessagesDto> GetConversationMessagesForAdminAsync(Guid customerId, SupportMessageQuery query)
    {
        var conversation = await _context.SupportConversations
            .AsNoTracking()
            .Include(c => c.Customer)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (conversation == null)
        {
            throw new KeyNotFoundException("Conversation not found");
        }

        var messages = await GetMessagesAsync(conversation.Id, query);
        return new SupportConversationMessagesDto
        {
            Conversation = MapConversation(conversation),
            Messages = messages
        };
    }

    public async Task<SupportConversationMessagesDto> GetMyConversationMessagesAsync(Guid customerId, SupportMessageQuery query)
    {
        var conversation = await _context.SupportConversations
            .AsNoTracking()
            .Include(c => c.Customer)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (conversation == null)
        {
            return new SupportConversationMessagesDto
            {
                Conversation = new SupportConversationDto
                {
                    CustomerId = customerId
                },
                Messages = new List<SupportMessageDto>()
            };
        }

        var messages = await GetMessagesAsync(conversation.Id, query);
        return new SupportConversationMessagesDto
        {
            Conversation = MapConversation(conversation),
            Messages = messages
        };
    }

    public async Task MarkConversationReadAsync(Guid viewerId, string viewerRole, Guid customerId)
    {
        var normalizedRole = viewerRole?.Trim() ?? string.Empty;
        var isAdmin = normalizedRole.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        var isCustomer = normalizedRole.Equals("Customer", StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && !isCustomer)
        {
            throw new UnauthorizedAccessException("Only Admin or Customer can read support conversation");
        }

        if (isCustomer && viewerId != customerId)
        {
            throw new UnauthorizedAccessException("Customer can only access their own conversation");
        }

        var conversation = await _context.SupportConversations
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);
        if (conversation == null)
        {
            return;
        }

        if (isAdmin)
        {
            conversation.AdminUnreadCount = 0;
        }
        else
        {
            conversation.CustomerUnreadCount = 0;
        }

        await _context.SaveChangesAsync();
    }

    private async Task<List<SupportMessageDto>> GetMessagesAsync(Guid conversationId, SupportMessageQuery query)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        return await _context.SupportMessages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new SupportMessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                SenderId = m.SenderId,
                SenderName = m.Sender.FullName,
                IsFromAdmin = m.IsFromAdmin,
                Content = m.Content,
                IsRead = m.IsRead,
                ReadAt = m.ReadAt,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();
    }

    private static SupportConversationDto MapConversation(SupportConversation conversation)
    {
        return new SupportConversationDto
        {
            Id = conversation.Id,
            CustomerId = conversation.CustomerId,
            CustomerName = conversation.Customer.FullName,
            CustomerEmail = conversation.Customer.Email,
            LastMessageAt = conversation.LastMessageAt,
            LastMessagePreview = conversation.LastMessagePreview,
            CustomerUnreadCount = conversation.CustomerUnreadCount,
            AdminUnreadCount = conversation.AdminUnreadCount,
            CreatedAt = conversation.CreatedAt
        };
    }
}
