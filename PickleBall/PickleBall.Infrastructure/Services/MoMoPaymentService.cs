using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PickleBall.Application.DTOs.Payments;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Services;

public class MoMoPaymentService : IMoMoPaymentService
{
    private readonly MoMoPaymentSettings _settings;
    private readonly ILogger<MoMoPaymentService> _logger;

    public MoMoPaymentService(IOptions<MoMoPaymentSettings> settings, ILogger<MoMoPaymentService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<CreatePaymentResponse> CreatePaymentAsync(Order order, string returnUrl, string cancelUrl, string notifyUrl)
    {
        try
        {
            // MoMo payment creation logic
            var requestId = Guid.NewGuid().ToString();
            var orderId = order.OrderNumber;
            var orderInfo = $"Thanh toan don hang {order.OrderNumber}";
            var amount = (long)order.TotalAmount; // MoMo amount is in VND (TotalAmount is already VND)
            var extraData = "";

            // Build raw signature string
            var rawHash = $"accessKey={_settings.AccessKey}&amount={amount}&extraData={extraData}&ipnUrl={_settings.IpnUrl}&orderId={orderId}&orderInfo={orderInfo}&partnerCode={_settings.PartnerCode}&redirectUrl={_settings.ReturnUrl}&requestId={requestId}&requestType=captureWallet";

            // Create HMAC SHA256 signature
            var signature = ComputeHmacSha256(rawHash, _settings.SecretKey);

            // Build payment request
            var paymentRequest = new
            {
                partnerCode = _settings.PartnerCode,
                accessKey = _settings.AccessKey,
                requestId = requestId,
                amount = amount,
                orderId = orderId,
                orderInfo = orderInfo,
                redirectUrl = _settings.ReturnUrl,
                ipnUrl = _settings.IpnUrl,
                extraData = extraData,
                requestType = "captureWallet",
                signature = signature
            };

            // Send request to MoMo API
            using var httpClient = new HttpClient();
            var json = JsonSerializer.Serialize(paymentRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(_settings.Endpoint, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("MoMo payment creation failed: {Response}", responseContent);
                throw new InvalidOperationException("Failed to create MoMo payment");
            }

            var paymentResponse = JsonSerializer.Deserialize<MoMoPaymentResponse>(responseContent);
            
            if (paymentResponse?.ResultCode == 0 && !string.IsNullOrEmpty(paymentResponse.PayUrl))
            {
                return new CreatePaymentResponse
                {
                    PaymentUrl = paymentResponse.PayUrl,
                    OrderId = order.Id,
                    OrderNumber = order.OrderNumber
                };
            }

            throw new InvalidOperationException($"MoMo payment creation failed: {paymentResponse?.Message ?? "Unknown error"}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating MoMo payment for order {OrderNumber}", order.OrderNumber);
            throw;
        }
    }

    public Task<bool> VerifyCallbackAsync(Dictionary<string, string> callbackData)
    {
        try
        {
            // An empty secret would make the HMAC trivially forgeable.
            if (string.IsNullOrEmpty(_settings.SecretKey) || string.IsNullOrEmpty(_settings.AccessKey))
            {
                _logger.LogError("MoMo secret is not configured; rejecting callback.");
                return Task.FromResult(false);
            }

            // Extract required fields from callback
            if (!callbackData.TryGetValue("partnerCode", out var partnerCode) ||
                !callbackData.TryGetValue("orderId", out var orderId) ||
                !callbackData.TryGetValue("amount", out var amountStr) ||
                !callbackData.TryGetValue("extraData", out var extraData) ||
                !callbackData.TryGetValue("requestId", out var requestId) ||
                !callbackData.TryGetValue("orderType", out var orderType) ||
                !callbackData.TryGetValue("transId", out var transId) ||
                !callbackData.TryGetValue("resultCode", out var resultCode) ||
                !callbackData.TryGetValue("message", out var message) ||
                !callbackData.TryGetValue("payType", out var payType) ||
                !callbackData.TryGetValue("responseTime", out var responseTime) ||
                !callbackData.TryGetValue("signature", out var signature))
            {
                return Task.FromResult(false);
            }

            // Build raw signature string for verification
            var rawHash = $"accessKey={_settings.AccessKey}&amount={amountStr}&extraData={extraData}&message={message}&orderId={orderId}&orderType={orderType}&partnerCode={partnerCode}&payType={payType}&requestId={requestId}&responseTime={responseTime}&resultCode={resultCode}&transId={transId}";

            // Compute signature
            var computedSignature = ComputeHmacSha256(rawHash, _settings.SecretKey);

            // Verify signature
            if (!PickleBall.Application.Security.CryptoUtil.SignatureEquals(computedSignature, signature))
            {
                _logger.LogWarning("MoMo callback signature mismatch");
                return Task.FromResult(false);
            }

            // Verify partner code
            if (partnerCode != _settings.PartnerCode)
            {
                _logger.LogWarning("MoMo callback partner code mismatch");
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying MoMo callback");
            return Task.FromResult(false);
        }
    }

    private static string ComputeHmacSha256(string message, string secretKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(messageBytes);
        return Convert.ToHexString(hashBytes).ToLower();
    }

    private class MoMoPaymentResponse
    {
        public int ResultCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string PayUrl { get; set; } = string.Empty;
    }
}
