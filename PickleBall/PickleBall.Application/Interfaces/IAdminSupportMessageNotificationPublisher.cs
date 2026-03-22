using PickleBall.Application.DTOs.Notifications;

namespace PickleBall.Application.Interfaces;

public interface IAdminSupportMessageNotificationPublisher
{
    /// <summary>
    /// Broadcasts when a customer sends a support message. Must not throw; logs errors internally.
    /// </summary>
    Task NotifyNewCustomerMessageAsync(
        NewSupportMessageNotificationDto notification,
        CancellationToken cancellationToken = default);
}
