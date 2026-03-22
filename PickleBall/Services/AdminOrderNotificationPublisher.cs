using Microsoft.AspNetCore.SignalR;
using PickleBall.Application.DTOs.Notifications;
using PickleBall.Application.Interfaces;
using PickleBall.Hubs;

namespace PickleBall.Services;

public class AdminOrderNotificationPublisher
    : IAdminOrderNotificationPublisher,
      IAdminSupportMessageNotificationPublisher
{
    private readonly IHubContext<AdminNotificationsHub> _hubContext;
    private readonly ILogger<AdminOrderNotificationPublisher> _logger;

    public AdminOrderNotificationPublisher(
        IHubContext<AdminNotificationsHub> hubContext,
        ILogger<AdminOrderNotificationPublisher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyNewOrderAsync(NewOrderNotificationDto notification, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients
                .Group(AdminNotificationsHub.AdminGroupName)
                .SendAsync("NewOrder", notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast new order notification for order {OrderId}", notification.OrderId);
        }
    }

    public async Task NotifyNewCustomerMessageAsync(
        NewSupportMessageNotificationDto notification,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients
                .Group(AdminNotificationsHub.AdminGroupName)
                .SendAsync("NewSupportMessage", notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to broadcast new support message notification for conversation {ConversationId}",
                notification.ConversationId);
        }
    }
}
