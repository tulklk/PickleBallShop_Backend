using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PickleBall.Application.DTOs.SupportChat;
using PickleBall.Application.Interfaces;

namespace PickleBall.Hubs;

[Authorize]
public class SupportChatHub : Hub
{
    private readonly ISupportChatService _supportChatService;

    public SupportChatHub(ISupportChatService supportChatService)
    {
        _supportChatService = supportChatService;
    }

    public async Task JoinConversation(Guid? customerId = null)
    {
        var userId = GetUserId();
        var role = GetUserRole();
        var targetCustomerId = ResolveTargetCustomerId(userId, role, customerId);
        var groupName = BuildConversationGroupName(targetCustomerId);

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await _supportChatService.MarkConversationReadAsync(userId, role, targetCustomerId);
    }

    public async Task LeaveConversation(Guid? customerId = null)
    {
        var userId = GetUserId();
        var role = GetUserRole();
        var targetCustomerId = ResolveTargetCustomerId(userId, role, customerId);
        var groupName = BuildConversationGroupName(targetCustomerId);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task SendMessage(Guid customerId, string content)
    {
        var userId = GetUserId();
        var role = GetUserRole();
        var message = await _supportChatService.SendMessageAsync(userId, role, customerId, content);
        var groupName = BuildConversationGroupName(customerId);

        await Clients.Group(groupName).SendAsync("ReceiveMessage", message);
    }

    public async Task MarkConversationRead(Guid? customerId = null)
    {
        var userId = GetUserId();
        var role = GetUserRole();
        var targetCustomerId = ResolveTargetCustomerId(userId, role, customerId);

        await _supportChatService.MarkConversationReadAsync(userId, role, targetCustomerId);
    }

    private Guid GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdClaim ?? throw new UnauthorizedAccessException("User is not authenticated"));
    }

    private string GetUserRole()
    {
        var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
        return role ?? throw new UnauthorizedAccessException("User role is missing");
    }

    private static Guid ResolveTargetCustomerId(Guid userId, string role, Guid? requestedCustomerId)
    {
        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            if (!requestedCustomerId.HasValue)
            {
                throw new HubException("Admin must provide customerId");
            }

            return requestedCustomerId.Value;
        }

        if (!role.Equals("Customer", StringComparison.OrdinalIgnoreCase))
        {
            throw new HubException("Only Admin or Customer can join support conversations");
        }

        if (requestedCustomerId.HasValue && requestedCustomerId.Value != userId)
        {
            throw new HubException("Customer can only access their own conversation");
        }

        return userId;
    }

    private static string BuildConversationGroupName(Guid customerId)
    {
        return $"conversation:{customerId}";
    }
}
