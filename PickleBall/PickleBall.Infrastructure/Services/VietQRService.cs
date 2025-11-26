using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PickleBall.Application.DTOs.Payments;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Services;

public class VietQRService : IVietQRService
{
    private readonly HttpClient _httpClient;
    private readonly VietQRSettings _settings;
    private readonly ILogger<VietQRService> _logger;
    
    // Bank BIN codes mapping
    private static readonly Dictionary<int, string> BankNames = new()
    {
        { 970415, "VietinBank" },
        { 970436, "Vietcombank" },
        { 970418, "BIDV" },
        { 970405, "Agribank" },
        { 970432, "VPBank" },
        { 970423, "TPBank" },
        { 970407, "Techcombank" },
        { 970422, "MB Bank" },
        { 970416, "ACB" },
        { 970448, "OCB" },
        { 970437, "HDBank" },
        { 970441, "VIB" },
        { 970443, "SHB" },
        { 970454, "VietCapitalBank" },
        { 970429, "SCB" },
        { 970431, "Eximbank" },
        { 970426, "MSB" },
        { 970414, "OceanBank" },
        { 970419, "NCB" },
        { 970428, "NamABank" },
        { 970403, "Sacombank" },
        { 970440, "SeABank" },
        { 970449, "BaoVietBank" },
        { 970425, "ABBank" },
        { 970427, "VietABank" },
        { 970439, "PVcomBank" },
        { 970424, "ShinhanBank" }
    };

    public VietQRService(
        HttpClient httpClient,
        IOptions<VietQRSettings> settings,
        ILogger<VietQRService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<BankTransferPaymentResponse> GenerateQRCodeAsync(Order order)
    {
        try
        {
            // Transfer content format: DH{OrderNumber suffix}
            // Remove "ORD-" prefix and keep last part for shorter content
            var orderSuffix = order.OrderNumber.Replace("ORD-", "").Replace("-", "");
            var transferContent = $"DH{orderSuffix}";
            
            // Ensure transfer content is max 25 characters (VietQR limit)
            if (transferContent.Length > 25)
            {
                transferContent = transferContent[..25];
            }

            var requestBody = new
            {
                accountNo = _settings.AccountNo,
                accountName = _settings.AccountName,
                acqId = _settings.AcqId,
                amount = (long)order.TotalAmount, // VietQR expects integer amount
                addInfo = transferContent,
                format = "text",
                template = _settings.Template
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            // Add VietQR authentication headers
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-client-id", _settings.ClientId);
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _settings.ApiKey);

            _logger.LogInformation("Generating VietQR for Order {OrderNumber}, Amount: {Amount}", 
                order.OrderNumber, order.TotalAmount);

            var response = await _httpClient.PostAsync(_settings.Endpoint, jsonContent);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("VietQR API Response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("VietQR API error: {StatusCode} - {Response}", 
                    response.StatusCode, responseContent);
                throw new Exception($"VietQR API error: {response.StatusCode}");
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var vietQRResponse = JsonSerializer.Deserialize<VietQRApiResponse>(responseContent, options);

            if (vietQRResponse?.Code != "00")
            {
                _logger.LogError("VietQR generation failed: {Code} - {Desc}", 
                    vietQRResponse?.Code, vietQRResponse?.Desc);
                throw new Exception($"VietQR generation failed: {vietQRResponse?.Desc}");
            }

            var bankName = BankNames.TryGetValue(_settings.AcqId, out var name) ? name : "Unknown Bank";

            return new BankTransferPaymentResponse
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                QrDataUrl = vietQRResponse.Data?.QrDataURL ?? string.Empty,
                QrCode = vietQRResponse.Data?.QrCode ?? string.Empty,
                BankName = bankName,
                AccountNo = _settings.AccountNo,
                AccountName = _settings.AccountName,
                Amount = order.TotalAmount,
                TransferContent = transferContent,
                IsBankTransfer = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating VietQR for Order {OrderId}", order.Id);
            throw;
        }
    }
}

