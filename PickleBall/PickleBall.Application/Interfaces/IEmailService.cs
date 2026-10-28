using PickleBall.Domain.Entities;

namespace PickleBall.Application.Interfaces;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string email, string fullName, string verificationToken);
    Task<bool> SendEmailAsync(string toEmail, string subject, string body);
    Task SendOrderConfirmationEmailAsync(Order order, string? clientIpAddress = null);
    Task SendGuestOrderConfirmationEmailAsync(Order order, string? clientIpAddress = null);
    Task SendPasswordResetLinkEmailAsync(string email, string fullName, string resetUrl);
}

