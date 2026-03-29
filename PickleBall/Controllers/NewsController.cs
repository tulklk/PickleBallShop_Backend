using Microsoft.AspNetCore.Mvc;
using PickleBall.Application.DTOs.News;
using PickleBall.Application.DTOs.Products;
using PickleBall.Application.Interfaces;

namespace PickleBall.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NewsController : ControllerBase
{
    private readonly INewsService _newsService;

    public NewsController(INewsService newsService)
    {
        _newsService = newsService;
    }

    /// <summary>Literal route before {idOrSlug} so "categories" is not treated as a slug.</summary>
    [HttpGet("categories")]
    public ActionResult<List<string>> GetCategories()
    {
        var categories = new List<string>
        {
            "Tin tức",
            "Kiến thức",
            "Đánh giá sản phẩm",
            "Kỹ thuật Pickleball",
            "Mẹo TIPS Pickleball",
            "Luật chơi Pickleball",
            "Khuyến mãi"
        };

        return Ok(categories);
    }

    /// <summary>
    /// Get all published news (public endpoint)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResultDto<NewsListDto>>> GetNews([FromQuery] NewsQueryDto query)
    {
        // Force only published news for public endpoint
        query.IsPublished = true;

        var result = await _newsService.GetNewsAsync(query);
        return Ok(result);
    }

    /// <summary>
    /// Get news by ID or slug (public endpoint - only published)
    /// </summary>
    [HttpGet("{idOrSlug}")]
    public async Task<ActionResult<NewsDto>> GetNews(string idOrSlug)
    {
        if (string.IsNullOrWhiteSpace(idOrSlug))
        {
            return BadRequest(new { success = false, message = "News ID or slug is required", traceId = HttpContext.TraceIdentifier });
        }

        NewsDto? news;

        // Try to parse as Guid first
        if (Guid.TryParse(idOrSlug, out var id))
        {
            news = await _newsService.GetNewsByIdAsync(id);
        }
        else
        {
            // Try slug
            news = await _newsService.GetNewsBySlugAsync(idOrSlug);
        }

        if (news == null)
        {
            return NotFound(new { success = false, message = "News not found", traceId = HttpContext.TraceIdentifier });
        }

        // Only return published news for public endpoint
        if (!news.IsPublished)
        {
            return NotFound(new { success = false, message = "News not found", traceId = HttpContext.TraceIdentifier });
        }

        // Increment view count
        await _newsService.IncrementViewCountAsync(news.Id);

        return Ok(news);
    }
}
