using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PickleBall.Application.DTOs.Home;
using PickleBall.Application.DTOs.News;
using PickleBall.Application.Interfaces;

namespace PickleBall.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HomeController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly INewsService _newsService;
    private readonly IMemoryCache _cache;

    public HomeController(
        IProductService productService,
        ICategoryService categoryService,
        INewsService newsService,
        IMemoryCache cache)
    {
        _productService = productService;
        _categoryService = categoryService;
        _newsService = newsService;
        _cache = cache;
    }

    /// <summary>
    /// Single round-trip for homepage: featured products, category tree, latest published news.
    /// </summary>
    [HttpGet("bootstrap")]
    public async Task<ActionResult<HomeBootstrapResponseDto>> GetBootstrap(
        [FromQuery] int featuredCount = 10,
        [FromQuery] int newsCount = 5)
    {
        featuredCount = Math.Clamp(featuredCount, 1, 50);
        newsCount = Math.Clamp(newsCount, 1, 20);

        var key = $"home:bootstrap:v1:{featuredCount}:{newsCount}";
        var result = await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(3);
            var featured = await _productService.GetFeaturedProductsAsync(featuredCount);
            var tree = await _categoryService.GetCategoryTreeAsync();
            var newsPage = await _newsService.GetNewsAsync(new NewsQueryDto
            {
                Page = 1,
                PageSize = newsCount,
                IsPublished = true
            });
            return new HomeBootstrapResponseDto
            {
                FeaturedProducts = featured,
                CategoryTree = tree,
                LatestNews = newsPage.Items
            };
        });

        return Ok(result);
    }
}
