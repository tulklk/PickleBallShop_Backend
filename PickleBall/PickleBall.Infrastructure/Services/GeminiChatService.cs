using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PickleBall.Application.DTOs.Chat;
using PickleBall.Application.DTOs.Products;
using PickleBall.Application.Interfaces;

namespace PickleBall.Infrastructure.Services;

public class GeminiChatService : IGeminiChatService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiChatService> _logger;
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly string _apiKey;
    private readonly string _modelName;
    private readonly string _baseUrl = "https://generativelanguage.googleapis.com/v1";
    private static DateTime? _lastProductCacheTime;
    private static string? _cachedProductInfo;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(30); // Cache 30 phút

    public GeminiChatService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GeminiChatService> logger,
        IProductService productService,
        ICategoryService categoryService)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _productService = productService;
        _categoryService = categoryService;
        
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

            // Build system prompt with context about the shop and products
            var systemPrompt = await BuildSystemPromptAsync();

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

    private async Task<string> BuildSystemPromptAsync()
    {
        // Lấy thông tin sản phẩm (có cache để tránh query mỗi lần)
        var productInfo = await GetProductInfoAsync();
        
        // Lấy thông tin danh mục
        var categories = await _categoryService.GetAllCategoriesAsync();
        var categoryInfo = string.Join(", ", categories.Select(c => c.Name));

        return $@"Bạn là trợ lý AI chuyên nghiệp của IKA PickleBall Shop - một cửa hàng chuyên bán các sản phẩm PickleBall tại Việt Nam.

THÔNG TIN VỀ CỬA HÀNG:
- Tên: IKA PickleBall Shop
- Chuyên bán: Vợt PickleBall, bóng, giày, và các phụ kiện PickleBall
- Dịch vụ: Trao đổi vợt cũ, thay đế giày, sửa vợt PickleBall

DANH MỤC SẢN PHẨM HIỆN CÓ:
{categoryInfo}

DANH SÁCH SẢN PHẨM TRONG CỬA HÀNG:
{productInfo}

KIẾN THỨC VỀ PICKLEBALL:
1. **Về bộ môn PickleBall:**
   - Pickleball là môn thể thao kết hợp giữa tennis, badminton và ping-pong
   - Chơi trên sân nhỏ hơn tennis, dùng vợt và bóng nhựa có lỗ
   - Phù hợp cho mọi lứa tuổi, từ trẻ em đến người cao tuổi
   - Được USAP (USA Pickleball Association) quản lý và chuẩn hóa

2. **Về vợt PickleBall:**
   - **Mặt vợt:** Carbon thô T700 (kiểm soát tốt, bền), Composite (cân bằng), Graphite (nhẹ, linh hoạt)
   - **Lõi vợt:** 
     * Polymer core (phổ biến, giá rẻ)
     * Nomex core (mạnh mẽ, độ bền cao)
     * Aluminum core (nhẹ, phản hồi tốt)
     * DD honeycomb core 16mm (cân bằng giữa sức mạnh và kiểm soát)
   - **Công nghệ:** Heat press (mở rộng điểm ngọt, giảm rung), Edge guard (bảo vệ viền)
   - **Trọng lượng:** 7-9 oz (nhẹ), 9-11 oz (trung bình), 11+ oz (nặng)
   - **Độ dài tay cầm:** 4-5 inch (ngắn, kiểm soát), 5+ inch (dài, lực mạnh)

3. **Về bóng PickleBall:**
   - Bóng trong nhà: 26 lỗ, mềm hơn, phù hợp sân trong nhà
   - Bóng ngoài trời: 40 lỗ, cứng hơn, chịu được gió
   - Màu sắc: Vàng, trắng, cam (theo quy định USAP)

4. **Về giày PickleBall:**
   - Cần giày có độ bám tốt, hỗ trợ bên hông (lateral support)
   - Đế không để lại vết trên sân
   - Thương hiệu phổ biến: ASICS, Nike, Adidas, K-Swiss

5. **Phụ kiện:**
   - Túi đựng vợt, băng tay, mũ, kính, quần áo thể thao

HƯỚNG DẪN TƯ VẤN VÀ GỢI Ý SẢN PHẨM:

1. **Khi khách hỏi về sản phẩm:**
   - Sử dụng DANH SÁCH SẢN PHẨM ở trên để trả lời chính xác
   - Nêu tên sản phẩm, giá, thương hiệu, đánh giá (nếu có)
   - Nếu sản phẩm có variants (màu, size), hãy đề cập đến
   - Khuyến khích khách xem chi tiết trên website

2. **Khi khách hỏi về pickleball:**
   - Giải thích chi tiết dựa trên KIẾN THỨC VỀ PICKLEBALL ở trên
   - Đưa ra lời khuyên phù hợp với trình độ (người mới, trung cấp, nâng cao)
   - Kết hợp kiến thức với sản phẩm có sẵn trong cửa hàng

3. **Khi khách cần gợi ý sản phẩm:**
   - Hỏi thêm về: trình độ chơi, sở thích (kiểm soát vs sức mạnh), ngân sách, mục đích sử dụng
   - Dựa vào DANH SÁCH SẢN PHẨM để gợi ý 2-3 sản phẩm phù hợp
   - So sánh các sản phẩm (ưu/nhược điểm, giá cả)
   - Đề xuất sản phẩm có đánh giá cao hoặc giá tốt

4. **Khi khách hỏi về dịch vụ:**
   - Trao đổi vợt cũ: Tiết kiệm chi phí, đổi mới, ký gửi
   - Thay đế giày: Giữ nguyên cấu trúc, chuẩn 100%, hỗ trợ nhiều thương hiệu
   - Sửa vợt: Chi phí thấp, không lệch, thẩm mỹ cao, có bảo hành

NGUYÊN TẮC TRẢ LỜI:
- Luôn trả lời bằng tiếng Việt một cách thân thiện và chuyên nghiệp
- Sử dụng thông tin sản phẩm THỰC TẾ từ DANH SÁCH SẢN PHẨM ở trên
- Nếu không tìm thấy sản phẩm trong danh sách, hãy nói rõ và gợi ý sản phẩm tương tự
- Khi gợi ý sản phẩm, luôn kèm theo giá và thông tin cơ bản
- Khuyến khích khách hàng xem chi tiết sản phẩm trên website
- Luôn giữ thái độ nhiệt tình và hỗ trợ tối đa
- Nếu không chắc chắn, hãy đề nghị khách hàng liên hệ trực tiếp

LƯU Ý QUAN TRỌNG:
- Bạn có thể mắc lỗi. Khách hàng nên kiểm tra thông tin quan trọng trước khi quyết định mua hàng
- Giá cả và tồn kho có thể thay đổi, khách nên kiểm tra trên website
- Luôn ưu tiên sản phẩm có sẵn trong DANH SÁCH SẢN PHẨM ở trên";
    }

    private async Task<string> GetProductInfoAsync()
    {
        // Kiểm tra cache
        if (_cachedProductInfo != null && 
            _lastProductCacheTime.HasValue && 
            DateTime.UtcNow - _lastProductCacheTime.Value < CacheExpiration)
        {
            return _cachedProductInfo;
        }

        try
        {
            // Lấy tất cả sản phẩm active (lấy nhiều để có đủ thông tin)
            var query = new ProductQueryDto
            {
                Page = 1,
                PageSize = 100 // Lấy tối đa 100 sản phẩm đầu tiên
            };

            var productsResult = await _productService.GetProductsAsync(query);
            var products = productsResult.Items;

            if (!products.Any())
            {
                return "Hiện tại cửa hàng chưa có sản phẩm nào.";
            }

            var productInfoBuilder = new StringBuilder();
            productInfoBuilder.AppendLine($"Tổng số sản phẩm: {productsResult.TotalCount}");
            productInfoBuilder.AppendLine();

            // Nhóm sản phẩm theo thương hiệu hoặc loại
            var groupedProducts = products
                .GroupBy(p => p.Brand ?? "Khác")
                .OrderBy(g => g.Key);

            foreach (var group in groupedProducts)
            {
                productInfoBuilder.AppendLine($"**{group.Key}:**");
                
                foreach (var product in group.OrderBy(p => p.Name))
                {
                    productInfoBuilder.Append($"- {product.Name}");
                    
                    if (!string.IsNullOrWhiteSpace(product.Brand))
                    {
                        productInfoBuilder.Append($" ({product.Brand})");
                    }
                    
                    productInfoBuilder.Append($" - Giá: {product.Price:N0} VNĐ");
                    
                    if (product.OriginalPrice.HasValue && product.OriginalPrice > product.Price)
                    {
                        var discount = ((product.OriginalPrice.Value - product.Price) / product.OriginalPrice.Value * 100);
                        productInfoBuilder.Append($" (Giảm {discount:F0}% từ {product.OriginalPrice.Value:N0} VNĐ)");
                    }
                    
                    if (product.AverageRating.HasValue && product.AverageRating > 0)
                    {
                        productInfoBuilder.Append($" - Đánh giá: {product.AverageRating:F1}/5 ({product.ReviewCount} đánh giá)");
                    }
                    
                    if (product.StockQuantity > 0)
                    {
                        productInfoBuilder.Append($" - Còn hàng: {product.StockQuantity} sản phẩm");
                    }
                    else
                    {
                        productInfoBuilder.Append(" - Hết hàng");
                    }
                    
                    productInfoBuilder.AppendLine();
                }
                
                productInfoBuilder.AppendLine();
            }

            // Lấy thêm featured products để có thông tin đầy đủ hơn
            var featuredProducts = await _productService.GetFeaturedProductsAsync(10);
            if (featuredProducts.Any())
            {
                var featuredNotInList = featuredProducts
                    .Where(fp => !products.Any(p => p.Id == fp.Id))
                    .ToList();
                
                if (featuredNotInList.Any())
                {
                    productInfoBuilder.AppendLine("**SẢN PHẨM NỔI BẬT:**");
                    foreach (var product in featuredNotInList)
                    {
                        productInfoBuilder.AppendLine($"- {product.Name} ({product.Brand ?? "N/A"}) - {product.Price:N0} VNĐ - Đánh giá: {product.AverageRating:F1}/5");
                    }
                    productInfoBuilder.AppendLine();
                }
            }

            _cachedProductInfo = productInfoBuilder.ToString();
            _lastProductCacheTime = DateTime.UtcNow;
            
            _logger.LogInformation("Product info cached at {Time}", _lastProductCacheTime);
            
            return _cachedProductInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching product info for AI context");
            return "Không thể tải thông tin sản phẩm lúc này. Vui lòng thử lại sau.";
        }
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

