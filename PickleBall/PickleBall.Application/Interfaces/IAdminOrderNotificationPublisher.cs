using PickleBall.Application.DTOs.Notifications;

namespace PickleBall.Application.Interfaces;

public interface IAdminOrderNotificationPublisher
{
    /// <summary>
    /// Broadcasts to connected admins. Must not throw; logs errors internally.
    /// </summary>
    Task NotifyNewOrderAsync(NewOrderNotificationDto notification, CancellationToken cancellationToken = default);
}
