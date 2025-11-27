using PickleBall.Application.DTOs.Payments;
using PickleBall.Domain.Entities;

namespace PickleBall.Application.Interfaces;

public interface IPayOSService
{
    /// <summary>
    /// Create PayOS payment link for bank transfer
    /// </summary>
    Task<CreatePaymentResponse> CreatePaymentLinkAsync(Order order, string returnUrl, string cancelUrl);
    
    /// <summary>
    /// Verify PayOS webhook data
    /// </summary>
    Task<PayOSWebhookData?> VerifyWebhookAsync(PayOSWebhookRequest webhookData);
}

