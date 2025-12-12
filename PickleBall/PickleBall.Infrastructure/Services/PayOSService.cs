using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PickleBall.Application.DTOs.Payments;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Services;

public class PayOSService : IPayOSService
{
    private readonly HttpClient _httpClient;
    private readonly PayOSSettings _settings;
    private readonly ILogger<PayOSService> _logger;
    private const string PAYOS_API_URL = "https://api-merchant.payos.vn/v2/payment-requests";
    
    // Static cache to store orderCode -> OrderId mapping
    // Key: PayOS orderCode, Value: (OrderId, Timestamp)
    private static readonly Dictionary<long, (Guid OrderId, DateTime Timestamp)> _orderCodeCache = new();
    private static readonly object _cacheLock = new();
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(2);

    public PayOSService(
        HttpClient httpClient,
        IOptions<PayOSSettings> settings,
        ILogger<PayOSService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<CreatePaymentResponse> CreatePaymentLinkAsync(Order order, string returnUrl, string cancelUrl)
    {
        try
        {
            var orderCode = GenerateOrderCode(order.OrderNumber);
            
            // Cache the orderCode -> OrderId mapping
            lock (_cacheLock)
            {
                _orderCodeCache[orderCode] = (order.Id, DateTime.UtcNow);
                CleanupExpiredCache();
            }
            
            _logger.LogInformation("Cached PayOS orderCode {OrderCode} for Order {OrderId}", orderCode, order.Id);
            
            var amount = (int)order.TotalAmount;
            var description = $"DH{order.OrderNumber.Replace("ORD-", "").Replace("-", "")}";
            
            // Truncate description to 25 chars max
            if (description.Length > 25)
                description = description[..25];

            var items = order.Items.Select(item => new
            {
                name = item.ProductNameSnapshot ?? "Product",
                quantity = item.Quantity,
                price = (int)item.UnitPrice
            }).ToList();

            // Create signature
            var signatureData = $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";
            var signature = ComputeHmacSha256(signatureData, _settings.ChecksumKey);

            var requestBody = new
            {
                orderCode = orderCode,
                amount = amount,
                description = description,
                items = items,
                cancelUrl = cancelUrl,
                returnUrl = returnUrl,
                signature = signature
            };

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-client-id", _settings.ClientId);
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _settings.ApiKey);

            _logger.LogInformation("Creating PayOS payment for Order {OrderNumber}, Amount: {Amount}", 
                order.OrderNumber, order.TotalAmount);

            var response = await _httpClient.PostAsJsonAsync(PAYOS_API_URL, requestBody);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("PayOS API Response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PayOS API error: {StatusCode} - {Response}", 
                    response.StatusCode, responseContent);
                throw new Exception($"PayOS API error: {response.StatusCode}");
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var payOSResponse = JsonSerializer.Deserialize<PayOSApiResponse>(responseContent, options);

            if (payOSResponse?.Code != "00")
            {
                _logger.LogError("PayOS creation failed: {Code} - {Desc}", 
                    payOSResponse?.Code, payOSResponse?.Desc);
                throw new Exception($"PayOS creation failed: {payOSResponse?.Desc}");
            }

            return new CreatePaymentResponse
            {
                PaymentUrl = payOSResponse.Data?.CheckoutUrl ?? string.Empty,
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                IsBankTransfer = true,
                BankTransferInfo = new BankTransferInfo
                {
                    QrDataUrl = payOSResponse.Data?.QrCode ?? string.Empty,
                    QrCode = payOSResponse.Data?.QrCode ?? string.Empty,
                    BankName = "PayOS",
                    AccountNo = payOSResponse.Data?.AccountNumber ?? string.Empty,
                    AccountName = payOSResponse.Data?.AccountName ?? string.Empty,
                    Amount = order.TotalAmount,
                    TransferContent = description
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating PayOS payment for Order {OrderId}", order.Id);
            throw;
        }
    }

    public Task<PayOSWebhookData?> VerifyWebhookAsync(PayOSWebhookRequest webhookData)
    {
        try
        {
            // Verify signature
            var data = webhookData.Data;
            if (data == null) return Task.FromResult<PayOSWebhookData?>(null);

            // Create signature from data
            var signatureData = $"amount={data.Amount}&code={data.Code}&desc={data.Desc}&orderCode={data.OrderCode}&paymentLinkId={data.PaymentLinkId}&reference={data.Reference}&transactionDateTime={data.TransactionDateTime}";
            var computedSignature = ComputeHmacSha256(signatureData, _settings.ChecksumKey);

            if (computedSignature != webhookData.Signature)
            {
                _logger.LogWarning("PayOS webhook signature mismatch");
                return Task.FromResult<PayOSWebhookData?>(null);
            }

            return Task.FromResult<PayOSWebhookData?>(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying PayOS webhook");
            return Task.FromResult<PayOSWebhookData?>(null);
        }
    }

    public static Guid? GetOrderIdFromOrderCode(long orderCode)
    {
        lock (_cacheLock)
        {
            CleanupExpiredCache();
            
            if (_orderCodeCache.TryGetValue(orderCode, out var cached))
            {
                return cached.OrderId;
            }
            
            return null;
        }
    }

    private static void CleanupExpiredCache()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _orderCodeCache
            .Where(kvp => now - kvp.Value.Timestamp > CacheExpiration)
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var key in expiredKeys)
        {
            _orderCodeCache.Remove(key);
        }
    }

    private long GenerateOrderCode(string orderNumber)
    {
        // Generate unique numeric code using timestamp + random
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var random = new Random().Next(1000, 9999);
        return (timestamp * 10000 + random) % 9007199254740991;
    }

    private static string ComputeHmacSha256(string data, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}

// PayOS API Response DTOs
public class PayOSApiResponse
{
    public string Code { get; set; } = string.Empty;
    public string Desc { get; set; } = string.Empty;
    public PayOSApiData? Data { get; set; }
}

public class PayOSApiData
{
    public string Bin { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public int Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public long OrderCode { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string PaymentLinkId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty;
}
