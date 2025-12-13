using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PickleBall.Application.DTOs.News;
using PickleBall.Application.DTOs.Products;
using PickleBall.Application.Interfaces;
using System.Security.Claims;

namespace PickleBall.Controllers.Admin;

[ApiController]
[Route("api/admin/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminNewsController : ControllerBase
{
    private readonly INewsService _newsService;

    public AdminNewsController(INewsService newsService)
    {
        _newsService = newsService;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdClaim ?? throw new UnauthorizedAccessException());
    }

    /// <summary>
    /// Get all news (including unpublished) - Admin only
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResultDto<NewsListDto>>> GetAllNews([FromQuery] NewsQueryDto query)
    {
        var result = await _newsService.GetNewsAsync(query);
        return Ok(result);
    }

    /// <summary>
    /// Get news by ID (including unpublished) - Admin only
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<NewsDto>> GetNewsById(Guid id)
    {
        var news = await _newsService.GetNewsByIdAsync(id);
        if (news == null)
        {
            return NotFound(new { message = "News not found" });
        }

        return Ok(news);
    }

    /// <summary>
    /// Create new news post - Admin only
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<NewsDto>> CreateNews([FromBody] CreateNewsRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var authorId = GetUserId();
            var news = await _newsService.CreateNewsAsync(authorId, request);
            return CreatedAtAction(nameof(GetNewsById), new { id = news.Id }, news);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update news post - Admin only
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<NewsDto>> UpdateNews(Guid id, [FromBody] UpdateNewsRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var news = await _newsService.UpdateNewsAsync(id, request);
            if (news == null)
            {
                return NotFound(new { message = "News not found" });
            }

            return Ok(news);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Delete news post - Admin only
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteNews(Guid id)
    {
        var deleted = await _newsService.DeleteNewsAsync(id);
        if (!deleted)
        {
            return NotFound(new { message = "News not found" });
        }

        return Ok(new { message = "News deleted successfully" });
    }
}

