using System.Collections.Specialized;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PickleBall.Application.DTOs.Payments;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Services;

public class VnPayPaymentService : IVnPayPaymentService
{
    private readonly VnPayPaymentSettings _settings;
    private readonly ILogger<VnPayPaymentService> _logger;

    public VnPayPaymentService(IOptions<VnPayPaymentSettings> settings, ILogger<VnPayPaymentService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public Task<CreatePaymentResponse> CreatePaymentAsync(Order order, string returnUrl, string cancelUrl, string notifyUrl)
    {
        try
        {
            // VNPay payment creation logic
            var vnpUrl = _settings.Endpoint;
            var vnpTmnCode = _settings.TmnCode;
            var vnpHashSecret = _settings.HashSecret;
            var vnpReturnUrl = _settings.ReturnUrl;
            var vnpOrderId = order.OrderNumber;
            var vnpAmount = (long)(order.TotalAmount * 100); // Convert to VND (order.TotalAmount is in VND, VNPay expects amount * 100)
            var vnpOrderInfo = $"Thanh toan don hang {order.OrderNumber}";
            var vnpOrderType = "other";
            var vnpLocale = "vn";
            var vnpIpAddr = "127.0.0.1"; // In production, get from request
            var vnpCreateDate = DateTime.Now.ToString("yyyyMMddHHmmss");
            var vnpTxnRef = order.OrderNumber;
            var vnpCurrCode = "VND";

            // Build query string
            var queryParams = new NameValueCollection
            {
                { "vnp_Version", "2.1.0" },
                { "vnp_Command", "pay" },
                { "vnp_TmnCode", vnpTmnCode },
                { "vnp_Amount", vnpAmount.ToString() },
                { "vnp_CurrCode", vnpCurrCode },
                { "vnp_TxnRef", vnpTxnRef },
                { "vnp_OrderInfo", vnpOrderInfo },
                { "vnp_OrderType", vnpOrderType },
                { "vnp_Locale", vnpLocale },
                { "vnp_ReturnUrl", vnpReturnUrl },
                { "vnp_IpAddr", vnpIpAddr },
                { "vnp_CreateDate", vnpCreateDate }
            };

            // Build query string for signature
            var sortedParams = queryParams.AllKeys.OrderBy(k => k);
            var signData = string.Join("&", sortedParams.Select(k => $"{System.Net.WebUtility.UrlEncode(k ?? "")}={System.Net.WebUtility.UrlEncode(queryParams[k] ?? "")}"));
            var vnpSecureHash = ComputeHmacSha512(signData, vnpHashSecret);
            queryParams.Add("vnp_SecureHash", vnpSecureHash);

            // Build full URL
            var allParams = queryParams.AllKeys.OrderBy(k => k);
            var queryString = string.Join("&", allParams.Select(k => $"{System.Net.WebUtility.UrlEncode(k ?? "")}={System.Net.WebUtility.UrlEncode(queryParams[k] ?? "")}"));
            var paymentUrl = $"{vnpUrl}?{queryString}";

            return Task.FromResult(new CreatePaymentResponse
            {
                PaymentUrl = paymentUrl,
                OrderId = order.Id,
                OrderNumber = order.OrderNumber
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating VNPay payment for order {OrderNumber}", order.OrderNumber);
            throw;
        }
    }

    public Task<bool> VerifyCallbackAsync(Dictionary<string, string> callbackData)
    {
        try
        {
            if (string.IsNullOrEmpty(_settings.HashSecret) || string.IsNullOrEmpty(_settings.TmnCode))
            {
                _logger.LogError("VNPay secret is not configured; rejecting callback.");
                return Task.FromResult(false);
            }

            // Extract vnp_SecureHash from callback
            if (!callbackData.TryGetValue("vnp_SecureHash", out var vnpSecureHash))
            {
                return Task.FromResult(false);
            }

            // Remove vnp_SecureHash from callback data for signature verification
            var dataForSignature = new Dictionary<string, string>(callbackData);
            dataForSignature.Remove("vnp_SecureHash");
            dataForSignature.Remove("vnp_SecureHashType");

            // Build query string for signature
            var queryString = string.Join("&", dataForSignature
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => $"{System.Net.WebUtility.UrlEncode(kvp.Key)}={System.Net.WebUtility.UrlEncode(kvp.Value)}"));

            // Compute signature
            var computedSignature = ComputeHmacSha512(queryString, _settings.HashSecret);

            // Verify signature
            if (!PickleBall.Application.Security.CryptoUtil.SignatureEquals(computedSignature, vnpSecureHash))
            {
                _logger.LogWarning("VNPay callback signature mismatch");
                return Task.FromResult(false);
            }

            // Verify transaction code
            if (!callbackData.TryGetValue("vnp_TmnCode", out var vnpTmnCode) || vnpTmnCode != _settings.TmnCode)
            {
                _logger.LogWarning("VNPay callback transaction code mismatch");
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying VNPay callback");
            return Task.FromResult(false);
        }
    }


    private static string ComputeHmacSha512(string message, string secretKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(messageBytes);
        return Convert.ToHexString(hashBytes).ToLower();
    }
}
