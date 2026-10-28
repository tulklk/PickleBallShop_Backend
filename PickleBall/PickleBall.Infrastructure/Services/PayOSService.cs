using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PickleBall.Application.DTOs.Payments;
using PickleBall.Application.Interfaces;
using PickleBall.Application.Security;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Services;

public class PayOSService : IPayOSService
{
    private readonly HttpClient _httpClient;
    private readonly PayOSSettings _settings;
    private readonly ILogger<PayOSService> _logger;
    private const string PAYOS_API_URL = "https://api-merchant.payos.vn/v2/payment-requests";

    public PayOSService(
        HttpClient httpClient,
        IOptions<PayOSSettings> settings,
        ILogger<PayOSService> logger)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(20);
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<CreatePaymentResponse> CreatePaymentLinkAsync(Order order, string returnUrl, string cancelUrl)
    {
        try
        {
            EnsureConfigured();

            var orderCode = GenerateOrderCode();
            // Persisted by the caller (Orders.PayOSOrderCode); this replaces the old in-memory cache.
            order.PayOSOrderCode = orderCode;

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

            using var request = new HttpRequestMessage(HttpMethod.Post, PAYOS_API_URL)
            {
                Content = JsonContent.Create(requestBody)
            };
            request.Headers.Add("x-client-id", _settings.ClientId);
            request.Headers.Add("x-api-key", _settings.ApiKey);

            _logger.LogInformation("Creating PayOS payment for Order {OrderNumber}, Amount: {Amount}",
                order.OrderNumber, order.TotalAmount);

            using var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PayOS API error: {StatusCode}", response.StatusCode);
                throw new InvalidOperationException("Payment provider error");
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var payOSResponse = JsonSerializer.Deserialize<PayOSApiResponse>(responseContent, options);

            if (payOSResponse?.Code != "00")
            {
                _logger.LogError("PayOS creation failed: {Code} - {Desc}",
                    payOSResponse?.Code, payOSResponse?.Desc);
                throw new InvalidOperationException("Payment provider error");
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
            var data = webhookData.Data;
            if (data == null || string.IsNullOrEmpty(webhookData.Signature) || string.IsNullOrEmpty(_settings.ChecksumKey))
            {
                return Task.FromResult<PayOSWebhookData?>(null);
            }

            // Official PayOS scheme: every field of "data", sorted alphabetically, key=value joined by '&'.
            var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["accountNumber"] = data.AccountNumber,
                ["amount"] = data.Amount.ToString(),
                ["code"] = data.Code,
                ["counterAccountBankId"] = data.CounterAccountBankId,
                ["counterAccountBankName"] = data.CounterAccountBankName,
                ["counterAccountName"] = data.CounterAccountName,
                ["counterAccountNumber"] = data.CounterAccountNumber,
                ["currency"] = data.Currency,
                ["desc"] = data.Desc,
                ["description"] = data.Description,
                ["orderCode"] = data.OrderCode.ToString(),
                ["paymentLinkId"] = data.PaymentLinkId,
                ["reference"] = data.Reference,
                ["transactionDateTime"] = data.TransactionDateTime,
                ["virtualAccountName"] = data.VirtualAccountName,
                ["virtualAccountNumber"] = data.VirtualAccountNumber
            };
            var canonical = string.Join("&", fields.Select(kv => $"{kv.Key}={kv.Value ?? string.Empty}"));
            var computed = ComputeHmacSha256(canonical, _settings.ChecksumKey);

            // Previous (subset) canonical form, kept so already-working integrations do not silently break.
            var legacy = $"amount={data.Amount}&code={data.Code}&desc={data.Desc}&orderCode={data.OrderCode}&paymentLinkId={data.PaymentLinkId}&reference={data.Reference}&transactionDateTime={data.TransactionDateTime}";
            var computedLegacy = ComputeHmacSha256(legacy, _settings.ChecksumKey);

            if (!CryptoUtil.SignatureEquals(computed, webhookData.Signature) &&
                !CryptoUtil.SignatureEquals(computedLegacy, webhookData.Signature))
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

    public async Task<PayOSPaymentInfo?> GetPaymentInfoAsync(long orderCode)
    {
        if (string.IsNullOrEmpty(_settings.ClientId) || string.IsNullOrEmpty(_settings.ApiKey))
        {
            _logger.LogError("PayOS credentials are not configured; cannot verify payment.");
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{PAYOS_API_URL}/{orderCode}");
            request.Headers.Add("x-client-id", _settings.ClientId);
            request.Headers.Add("x-api-key", _settings.ApiKey);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("PayOS status lookup failed with {StatusCode}", response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync();
            var parsed = JsonSerializer.Deserialize<PayOSApiResponse>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsed?.Code != "00" || parsed.Data == null || parsed.Data.OrderCode != orderCode)
            {
                return null;
            }

            return new PayOSPaymentInfo
            {
                OrderCode = parsed.Data.OrderCode,
                Amount = parsed.Data.Amount,
                AmountPaid = parsed.Data.AmountPaid,
                Status = parsed.Data.Status ?? string.Empty,
                PaymentLinkId = parsed.Data.PaymentLinkId ?? parsed.Data.Id
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up PayOS payment {OrderCode}", orderCode);
            return null;
        }
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrEmpty(_settings.ClientId) ||
            string.IsNullOrEmpty(_settings.ApiKey) ||
            string.IsNullOrEmpty(_settings.ChecksumKey))
        {
            throw new InvalidOperationException("Payment provider is not configured");
        }
    }

    private static long GenerateOrderCode()
    {
        // Unique numeric code: millisecond timestamp + CSPRNG suffix, kept within JS safe-integer range.
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var random = RandomNumberGenerator.GetInt32(1000, 9999);
        return (timestamp * 10000 + random) % 9007199254740991;
    }

    private static string ComputeHmacSha256(string data, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant();
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
    public string Id { get; set; } = string.Empty;
    public string Bin { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public long Amount { get; set; }
    public long AmountPaid { get; set; }
    public string Description { get; set; } = string.Empty;
    public long OrderCode { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string PaymentLinkId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty;
}
