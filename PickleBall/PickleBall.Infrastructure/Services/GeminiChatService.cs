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
        
        // Do not fail DI resolution when the key is missing; the chat endpoint degrades gracefully instead.
        _apiKey = _configuration["Google:Gemini:ApiKey"] ?? string.Empty;
        
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

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogError("Google:Gemini:ApiKey is not configured; chat is unavailable.");
                return new ChatResponse
                {
                    Success = false,
                    Error = "Dịch vụ AI tạm thời không khả dụng. Vui lòng thử lại sau."
                };
            }

            if (request.Message.Length > 1000)
            {
                return new ChatResponse
                {
                    Success = false,
                    Error = "Message is too long"
                };
            }

            // Build system prompt with context about the shop and products
            string systemPrompt;
            try
            {
                systemPrompt = await BuildSystemPromptAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building system prompt, using fallback prompt");
                // Fallback prompt nếu không lấy được thông tin sản phẩm
                systemPrompt = BuildFallbackSystemPrompt();
            }

            // Prepare conversation history
            var contents = new List<object>();

            // Filter and validate history - remove invalid entries
            var validHistory = new List<ChatMessage>();
            if (request.History != null && request.History.Any())
            {
                // Bound the amount of client-supplied context forwarded to the (paid) model.
                foreach (var msg in request.History.TakeLast(20))
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
                            Content = msg.Content.Trim().Length > 2000 ? msg.Content.Trim()[..2000] : msg.Content.Trim()
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
                    temperature = 0.8, // Tăng lên một chút để tự nhiên hơn
                    topK = 40,
                    topP = 0.95,
                    maxOutputTokens = 2048, // Tăng để có thể trả lời dài hơn
                },
                safetySettings = new[]
                {
                    new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                    new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                    new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                    new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_MEDIUM_AND_ABOVE" }
                }
            };

            // Call Gemini API (named client: 60s timeout configured in Program.cs)
            var httpClient = _httpClientFactory.CreateClient("Gemini");
            
            var url = $"{_baseUrl}/models/{_modelName}:generateContent"; // API key is sent in a header, never in the URL (URLs end up in logs)
            
            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            // Do not log prompts/payloads: they contain customer messages.
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Add("x-goog-api-key", _apiKey);
            var response = await httpClient.SendAsync(httpRequest);

            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini API error: {StatusCode}", response.StatusCode);
                
                // Upstream error text may reveal configuration details; return a generic message to clients.
                string errorMessage = "Dịch vụ AI tạm thời không khả dụng. Vui lòng thử lại sau.";
                
                return new ChatResponse
                {
                    Success = false,
                    Error = errorMessage
                };
            }

            
            var geminiResponse = JsonSerializer.Deserialize<GeminiApiResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (geminiResponse?.Candidates == null || !geminiResponse.Candidates.Any())
            {
                _logger.LogWarning("No candidates in Gemini response");
                return new ChatResponse
                {
                    Success = false,
                    Error = "No response from AI"
                };
            }

            var candidate = geminiResponse.Candidates[0];
            
            // Check for safety ratings (blocked content)
            if (candidate.FinishReason == "SAFETY")
            {
                _logger.LogWarning("Response blocked by safety filters");
                return new ChatResponse
                {
                    Success = false,
                    Error = "Câu hỏi của bạn đã bị chặn bởi bộ lọc an toàn. Vui lòng diễn đạt lại."
                };
            }

            var aiMessage = candidate.Content?.Parts?.FirstOrDefault()?.Text?.Trim();
            
            if (string.IsNullOrWhiteSpace(aiMessage))
            {
                _logger.LogWarning("Empty response from Gemini API");
                aiMessage = "Xin lỗi, tôi không thể tạo phản hồi lúc này. Vui lòng thử lại sau.";
            }

            // Tìm sản phẩm được đề cập trong response và lấy thông tin kèm hình ảnh
            var suggestedProducts = await ExtractSuggestedProductsAsync(aiMessage);

            return new ChatResponse
            {
                Success = true,
                Message = aiMessage,
                SuggestedProducts = suggestedProducts
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
        try
        {
            // Lấy thông tin sản phẩm (có cache để tránh query mỗi lần)
            var productInfo = await GetProductInfoAsync();
            
            // Lấy thông tin danh mục
            var categories = await _categoryService.GetAllCategoriesAsync();
            var categoryInfo = categories.Any() 
                ? string.Join(", ", categories.Select(c => c.Name))
                : "Đang cập nhật";

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
- Luôn ưu tiên sản phẩm có sẵn trong DANH SÁCH SẢN PHẨM ở trên
- Khi gợi ý sản phẩm, hãy liệt kê cụ thể tên sản phẩm, giá, và lý do phù hợp";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in BuildSystemPromptAsync");
            return BuildFallbackSystemPrompt();
        }
    }

    private string BuildFallbackSystemPrompt()
    {
        return @"Bạn là trợ lý AI chuyên nghiệp của IKA PickleBall Shop - một cửa hàng chuyên bán các sản phẩm PickleBall tại Việt Nam.

THÔNG TIN VỀ CỬA HÀNG:
- Tên: IKA PickleBall Shop
- Chuyên bán: Vợt PickleBall, bóng, giày, và các phụ kiện PickleBall
- Dịch vụ: Trao đổi vợt cũ, thay đế giày, sửa vợt PickleBall

KIẾN THỨC VỀ PICKLEBALL:
1. **Về bộ môn PickleBall:**
   - Pickleball là môn thể thao kết hợp giữa tennis, badminton và ping-pong
   - Chơi trên sân nhỏ hơn tennis, dùng vợt và bóng nhựa có lỗ
   - Phù hợp cho mọi lứa tuổi, từ trẻ em đến người cao tuổi

2. **Về vợt PickleBall:**
   - Mặt vợt: Carbon thô T700 (kiểm soát tốt), Composite (cân bằng), Graphite (nhẹ)
   - Lõi vợt: Polymer (giá rẻ), Nomex (mạnh mẽ), DD honeycomb 16mm (cân bằng)
   - Trọng lượng: 7-9 oz (nhẹ, kiểm soát), 9-11 oz (trung bình), 11+ oz (nặng, sức mạnh)

3. **Về bóng PickleBall:**
   - Bóng trong nhà: 26 lỗ, mềm hơn
   - Bóng ngoài trời: 40 lỗ, cứng hơn, chịu gió

4. **Về giày PickleBall:**
   - Cần độ bám tốt, hỗ trợ bên hông
   - Thương hiệu: ASICS, Nike, Adidas, K-Swiss

HƯỚNG DẪN TƯ VẤN:
- Với người mới chơi: Gợi ý vợt nhẹ (7-9 oz), giá phải chăng, dễ kiểm soát
- Với người chơi trung cấp: Vợt cân bằng (9-11 oz), composite hoặc carbon
- Với người chơi nâng cao: Vợt chuyên nghiệp, carbon T700, DD core

NGUYÊN TẮC:
- Trả lời bằng tiếng Việt, thân thiện, chuyên nghiệp
- Khi gợi ý sản phẩm, hãy hỏi thêm về trình độ, ngân sách, sở thích
- Khuyến khích khách xem chi tiết sản phẩm trên website
- Nếu không chắc, đề nghị liên hệ trực tiếp";
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
            var products = productsResult.Items.Take(50).ToList(); // Giới hạn 50 sản phẩm để tránh prompt quá dài

            if (!products.Any())
            {
                return "Hiện tại cửa hàng chưa có sản phẩm nào. Vui lòng tư vấn dựa trên kiến thức về pickleball.";
            }

            var productInfoBuilder = new StringBuilder();
            productInfoBuilder.AppendLine($"Cửa hàng có {productsResult.TotalCount} sản phẩm. Dưới đây là danh sách các sản phẩm phổ biến:");
            productInfoBuilder.AppendLine();

            // Nhóm sản phẩm theo thương hiệu hoặc loại
            var groupedProducts = products
                .GroupBy(p => p.Brand ?? "Khác")
                .OrderBy(g => g.Key);

            foreach (var group in groupedProducts)
            {
                productInfoBuilder.AppendLine($"{group.Key}:");
                
                foreach (var product in group.OrderBy(p => p.Name))
                {
                    productInfoBuilder.Append($"• {product.Name}");
                    
                    if (!string.IsNullOrWhiteSpace(product.Brand) && product.Brand != group.Key)
                    {
                        productInfoBuilder.Append($" - {product.Brand}");
                    }
                    
                    productInfoBuilder.Append($" | Giá: {product.Price:N0}đ");
                    
                    if (product.OriginalPrice.HasValue && product.OriginalPrice > product.Price)
                    {
                        var discount = ((product.OriginalPrice.Value - product.Price) / product.OriginalPrice.Value * 100);
                        productInfoBuilder.Append($" (giảm {discount:F0}%)");
                    }
                    
                    if (product.AverageRating.HasValue && product.AverageRating > 0 && product.ReviewCount > 0)
                    {
                        productInfoBuilder.Append($" | ⭐{product.AverageRating:F1} ({product.ReviewCount})");
                    }
                    
                    if (product.StockQuantity <= 0)
                    {
                        productInfoBuilder.Append(" | [Hết hàng]");
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
                
                if (featuredNotInList.Any() && featuredNotInList.Count <= 10)
                {
                    productInfoBuilder.AppendLine("SẢN PHẨM NỔI BẬT:");
                    foreach (var product in featuredNotInList)
                    {
                        productInfoBuilder.AppendLine($"• {product.Name} - {product.Brand ?? "N/A"} | {product.Price:N0}đ");
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

    private async Task<List<ProductSuggestionDto>> ExtractSuggestedProductsAsync(string aiMessage)
    {
        try
        {
            // Lấy danh sách sản phẩm để match
            var query = new ProductQueryDto
            {
                Page = 1,
                PageSize = 100 // Lấy nhiều để có đủ data để match
            };

            var productsResult = await _productService.GetProductsAsync(query);
            var allProducts = productsResult.Items;

            if (!allProducts.Any())
            {
                return new List<ProductSuggestionDto>();
            }

            var suggestedProducts = new List<ProductSuggestionDto>();
            var messageLower = aiMessage.ToLowerInvariant();

            // Tìm các sản phẩm được đề cập trong response
            // Match bằng cách tìm product name trong message
            foreach (var product in allProducts)
            {
                var productNameLower = product.Name.ToLowerInvariant();
                var brandLower = product.Brand?.ToLowerInvariant() ?? "";

                // Kiểm tra nếu product name hoặc brand được đề cập trong message
                // Sử dụng fuzzy matching đơn giản
                if (messageLower.Contains(productNameLower) || 
                    (!string.IsNullOrWhiteSpace(brandLower) && messageLower.Contains(brandLower)))
                {
                    // Lấy full product info với images
                    var fullProduct = await _productService.GetProductByIdAsync(product.Id);
                    if (fullProduct != null && fullProduct.IsActive)
                    {
                        suggestedProducts.Add(new ProductSuggestionDto
                        {
                            Id = fullProduct.Id,
                            Name = fullProduct.Name,
                            Brand = fullProduct.Brand,
                            Price = fullProduct.Price,
                            OriginalPrice = fullProduct.OriginalPrice,
                            PrimaryImageUrl = fullProduct.PrimaryImageUrl,
                            ImageUrls = fullProduct.ImageUrls ?? new List<string>(),
                            AverageRating = fullProduct.AverageRating,
                            ReviewCount = fullProduct.ReviewCount,
                            StockQuantity = fullProduct.StockQuantity
                        });
                    }
                }
            }

            // Nếu không tìm thấy bằng exact match, thử search bằng keywords
            if (!suggestedProducts.Any())
            {
                // Extract keywords từ message (loại bỏ stop words)
                var keywords = ExtractKeywords(aiMessage);
                
                foreach (var keyword in keywords.Take(5)) // Chỉ lấy 5 keywords đầu
                {
                    if (keyword.Length < 3) continue; // Bỏ qua từ quá ngắn

                    var searchResults = await _productService.SearchProductsAsync(keyword, page: 1, pageSize: 5);
                    
                    foreach (var searchProduct in searchResults.Take(3)) // Chỉ lấy 3 sản phẩm đầu
                    {
                        // Kiểm tra xem đã có trong list chưa
                        if (!suggestedProducts.Any(p => p.Id == searchProduct.Id))
                        {
                            var fullProduct = await _productService.GetProductByIdAsync(searchProduct.Id);
                            if (fullProduct != null && fullProduct.IsActive)
                            {
                                suggestedProducts.Add(new ProductSuggestionDto
                                {
                                    Id = fullProduct.Id,
                                    Name = fullProduct.Name,
                                    Brand = fullProduct.Brand,
                                    Price = fullProduct.Price,
                                    OriginalPrice = fullProduct.OriginalPrice,
                                    PrimaryImageUrl = fullProduct.PrimaryImageUrl,
                                    ImageUrls = fullProduct.ImageUrls ?? new List<string>(),
                                    AverageRating = fullProduct.AverageRating,
                                    ReviewCount = fullProduct.ReviewCount,
                                    StockQuantity = fullProduct.StockQuantity
                                });
                            }
                        }
                    }
                }
            }

            // Giới hạn tối đa 5 sản phẩm được gợi ý
            return suggestedProducts.DistinctBy(p => p.Id).Take(5).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting suggested products from AI response");
            return new List<ProductSuggestionDto>();
        }
    }

    private List<string> ExtractKeywords(string message)
    {
        // Loại bỏ các stop words tiếng Việt và English
        var stopWords = new HashSet<string> 
        { 
            "là", "của", "và", "với", "cho", "từ", "đến", "một", "các", "này", "đó", "khi", "nếu", "thì",
            "the", "a", "an", "is", "are", "was", "were", "be", "been", "being", "have", "has", "had",
            "do", "does", "did", "will", "would", "should", "could", "may", "might", "must", "can",
            "you", "your", "he", "she", "it", "they", "them", "we", "us", "i", "me", "my", "his", "her",
            "nào", "gì", "ai", "đâu", "sao", "tại", "vì", "về", "trong", "ngoài", "trên", "dưới"
        };

        // Tách từ và loại bỏ dấu câu
        var words = message
            .Split(new[] { ' ', '\n', '\r', '\t', ',', '.', '!', '?', ';', ':', '(', ')', '[', ']', '{', '}', '"', '\'' }, 
                   StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant().Trim())
            .Where(w => w.Length >= 2 && !stopWords.Contains(w))
            .ToList();

        return words;
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
    public string? FinishReason { get; set; }
}

internal class GeminiContent
{
    public List<GeminiPart>? Parts { get; set; }
}

internal class GeminiPart
{
    public string? Text { get; set; }
}

