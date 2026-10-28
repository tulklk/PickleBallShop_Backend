using PickleBall.Application.DTOs.Payments;
using PickleBall.Domain.Entities;

namespace PickleBall.Application.Interfaces;

public interface IPayOSService
{
    /// <summary>
    /// Create PayOS payment link for bank transfer. Sets <see cref="Order.PayOSOrderCode"/> on the
    /// (tracked) order; the caller must persist it.
    /// </summary>
    Task<CreatePaymentResponse> CreatePaymentLinkAsync(Order order, string returnUrl, string cancelUrl);

    /// <summary>
    /// Verify PayOS webhook data (HMAC signature). Returns null when invalid.
    /// </summary>
    Task<PayOSWebhookData?> VerifyWebhookAsync(PayOSWebhookRequest webhookData);

    /// <summary>
    /// Server-to-server lookup of a payment request at PayOS. The ONLY trustworthy way to confirm a payment
    /// when the browser returns from the gateway. Returns null when PayOS cannot be reached or the code is unknown.
    /// </summary>
    Task<PayOSPaymentInfo?> GetPaymentInfoAsync(long orderCode);
}

public class PayOSPaymentInfo
{
    public long OrderCode { get; set; }
    public long Amount { get; set; }
    public long AmountPaid { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PaymentLinkId { get; set; }

    public bool IsPaid => string.Equals(Status, "PAID", StringComparison.OrdinalIgnoreCase);
}
