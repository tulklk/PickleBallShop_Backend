using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PickleBall.Hubs;

/// <summary>
/// Realtime channel for admin-only events (e.g. new orders).
/// </summary>
[Authorize(Roles = "Admin")]
public class AdminNotificationsHub : Hub
{
    public const string AdminGroupName = "adminNotifications";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole("Admin") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroupName);
        }

        await base.OnConnectedAsync();
    }
}
