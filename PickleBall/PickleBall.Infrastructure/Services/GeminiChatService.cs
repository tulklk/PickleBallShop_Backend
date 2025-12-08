using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PickleBall.Application.DTOs.Chat;
using PickleBall.Application.Interfaces;

namespace PickleBall.Infrastructure.Services;

public class GeminiChatService : IGeminiChatService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiChatService> _logger;
    private readonly string _apiKey;
    private readonly string _modelName;
    private readonly string _baseUrl = "https://generativelanguage.googleapis.com/v1";

    public GeminiChatService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GeminiChatService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        
        _apiKey = _configuration["Google:Gemini:ApiKey"] 
            ?? throw new InvalidOperationException("Google Gemini API key is not configured");
        
        // Use gemini-2.5-flash as default (latest flash model)
        _modelName = _configuration["Google:Gemini:ModelName"] ?? "gemini-2.5-flash";
        
        _logger.LogInformation("Gemini Chat Service initialized with model: {ModelName}", _modelName);
    }

    public async Task<ChatResponse> SendMessageAsync(ChatRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return new ChatResponse
                {
                    Success = false,
                    Error = "Message cannot be empty"
                };
            }

            // Build system prompt with context about the shop
            var systemPrompt = BuildSystemPrompt();

            // Prepare conversation history
            var contents = new List<object>();

            // Filter and validate history - remove invalid entries
            var validHistory = new List<ChatMessage>();
            if (request.History != null && request.History.Any())
            {
                foreach (var msg in request.History)
                {
                    // Skip placeholder messages from Swagger
                    if (!string.IsNullOrWhiteSpace(msg.Role) && 
                        !string.IsNullOrWhiteSpace(msg.Content) &&
                        msg.Role != "string" && 
                        msg.Content != "string")
                    {
                        // Ensure role is either "user" or "model"
                        var role = msg.Role.ToLower() == "assistant" ? "model" : 
                                   msg.Role.ToLower() == "model" ? "model" : "user";
                        
                        validHistory.Add(new ChatMessage
                        {
                            Role = role,
                            Content = msg.Content.Trim()
                        });
                    }
                }
            }

            // Check if this is a new conversation (no valid history)
            bool isNewConversation = !validHistory.Any();

            if (isNewConversation)
            {
                // First message - include system prompt
                contents.Add(new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = systemPrompt }
                    }
                });
                
                // Add model's greeting response
                contents.Add(new
                {
                    role = "model",
                    parts = new[]
                    {
                        new { text = "Xin chào! Tôi là trợ lý AI của PickleBall Shop. Tôi có thể giúp bạn tìm hiểu về các sản phẩm pickleball, tư vấn chọn vợt, bóng, và các phụ kiện. Bạn cần hỗ trợ gì hôm nay?" }
                    }
                });
            }
            else
            {
                // Add system context only if not already in history
                var firstMessage = validHistory.FirstOrDefault();
                if (firstMessage == null || !firstMessage.Content.Contains("IKA PickleBall Shop"))
                {
                    contents.Add(new
                    {
                        role = "user",
                        parts = new[]
                        {
                            new { text = systemPrompt }
                        }
                    });
                }
                
                // Add existing conversation history
                foreach (var message in validHistory)
                {
                    contents.Add(new
                    {
                        role = message.Role,
                        parts = new[]
                        {
                            new { text = message.Content }
                        }
                    });
                }
            }

            // Add current user message
            contents.Add(new
            {
                role = "user",
                parts = new[]
                {
                    new { text = request.Message }
                }
            });

            // Build request payload - Gemini API uses camelCase
            var payload = new
            {
                contents = contents,
                generationConfig = new
                {
                    temperature = 0.7,
                    topK = 40,
                    topP = 0.95,
                    maxOutputTokens = 1024,
                }
            };

            // Call Gemini API
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(60); // Increase timeout for AI requests
            
            var url = $"{_baseUrl}/models/{_modelName}:generateContent?key={_apiKey}";
            
            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            _logger.LogInformation("Sending request to Gemini API: {Url}", url.Replace(_apiKey, "***"));
            _logger.LogDebug("Request payload: {Payload}", jsonPayload);

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(url, content);

            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini API error: {StatusCode} - {Error}", response.StatusCode, responseContent);
                
                // Try to extract error message from Gemini response
                string errorMessage = "Failed to get response from AI service";
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    if (errorResponse.TryGetProperty("error", out var errorObj))
                    {
                        if (errorObj.TryGetProperty("message", out var message))
                        {
                            errorMessage = message.GetString() ?? errorMessage;
                        }
                    }
                }
                catch
                {
                    // Use default error message
                }
                
                return new ChatResponse
                {
                    Success = false,
                    Error = errorMessage
                };
            }

            _logger.LogDebug("Gemini API response: {Response}", responseContent);
            
            var geminiResponse = JsonSerializer.Deserialize<GeminiApiResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (geminiResponse?.Candidates == null || !geminiResponse.Candidates.Any())
            {
                return new ChatResponse
                {
                    Success = false,
                    Error = "No response from AI"
                };
            }

            var aiMessage = geminiResponse.Candidates[0].Content?.Parts?.FirstOrDefault()?.Text ?? "Xin lỗi, tôi không thể tạo phản hồi lúc này.";

            return new ChatResponse
            {
                Success = true,
                Message = aiMessage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Gemini API");
            return new ChatResponse
            {
                Success = false,
                Error = "An error occurred while processing your request"
            };
        }
    }

    private string BuildSystemPrompt()
    {
        return @"Bạn là trợ lý AI chuyên nghiệp của IKA PickleBall Shop - một cửa hàng chuyên bán các sản phẩm PickleBall tại Việt Nam.

THÔNG TIN VỀ CỬA HÀNG:
- Tên: IKA PickleBall Shop
- Chuyên bán: Vợt PickleBall, bóng, giày, và các phụ kiện PickleBall
- Dịch vụ: Trao đổi vợt cũ, thay đế giày, sửa vợt PickleBall

BẠN CÓ THỂ HỖ TRỢ:
1. Tư vấn về các loại vợt PickleBall (LEGENDTEK Collection và các thương hiệu khác)
2. Hướng dẫn chọn size giày phù hợp
3. Tư vấn về bóng và phụ kiện
4. Giải thích về dịch vụ trao đổi vợt cũ, thay đế giày, sửa vợt
5. Thông tin về các sản phẩm nổi bật
6. Hướng dẫn chăm sóc và bảo quản vợt

NGUYÊN TẮC:
- Luôn trả lời bằng tiếng Việt một cách thân thiện và chuyên nghiệp
- Nếu không chắc chắn về thông tin, hãy đề nghị khách hàng liên hệ trực tiếp
- Khuyến khích khách hàng xem chi tiết sản phẩm trên website
- Luôn giữ thái độ nhiệt tình và hỗ trợ tối đa

Hãy luôn nhắc nhở khách hàng rằng bạn có thể mắc lỗi và khách hàng nên kiểm tra thông tin quan trọng trước khi quyết định mua hàng.";
    }
}

// Response models for Gemini API
internal class GeminiApiResponse
{
    public List<GeminiCandidate>? Candidates { get; set; }
}

internal class GeminiCandidate
{
    public GeminiContent? Content { get; set; }
}

internal class GeminiContent
{
    public List<GeminiPart>? Parts { get; set; }
}

internal class GeminiPart
{
    public string? Text { get; set; }
}

