using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PickleBall.Application.DTOs.Comments;
using PickleBall.Application.Interfaces;
using System.Security.Claims;

namespace PickleBall.Controllers;

[ApiController]
[Route("api/products/slug/{slug}/comments")]
public class ProductCommentsBySlugController : ControllerBase
{
    private readonly IProductCommentService _commentService;

    public ProductCommentsBySlugController(IProductCommentService commentService)
    {
        _commentService = commentService;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdClaim ?? throw new UnauthorizedAccessException());
    }

    private bool IsAdmin()
    {
        return User.IsInRole("Admin");
    }

    /// <summary>
    /// Get all comments for a product by slug (SEO-friendly URL, public endpoint)
    /// Example: GET /api/products/slug/ao-polo-pickleball-performance/comments
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<CommentsResponseDto>> GetProductCommentsBySlug(string slug)
    {
        try
        {
            var result = await _commentService.GetProductCommentsBySlugAsync(slug);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Create a new comment by product slug (requires authentication)
    /// Example: POST /api/products/slug/ao-polo-pickleball-performance/comments
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<CommentDto>> CreateCommentBySlug(string slug, [FromBody] CreateCommentRequest request)
    {
        try
        {
            var userId = GetUserId();
            var comment = await _commentService.CreateCommentBySlugAsync(userId, slug, request);
            return CreatedAtAction(nameof(GetProductCommentsBySlug), new { slug }, comment);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Reply to a comment by product slug (requires authentication - Any authenticated user can reply)
    /// Example: POST /api/products/slug/ao-polo-pickleball-performance/comments/{parentCommentId}/reply
    /// </summary>
    [HttpPost("{parentCommentId}/reply")]
    [Authorize]
    public async Task<ActionResult<CommentDto>> ReplyToCommentBySlug(
        string slug,
        Guid parentCommentId,
        [FromBody] ReplyCommentRequest request)
    {
        try
        {
            var userId = GetUserId();
            var reply = await _commentService.ReplyToCommentBySlugAsync(userId, slug, parentCommentId, request);
            return Ok(reply);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update own comment (requires authentication)
    /// Note: CommentId is sufficient, slug is used for consistency with route structure
    /// </summary>
    [HttpPut("{commentId}")]
    [Authorize]
    public async Task<ActionResult> UpdateComment(
        string slug,
        Guid commentId,
        [FromBody] CreateCommentRequest request)
    {
        try
        {
            var userId = GetUserId();
            var updated = await _commentService.UpdateCommentAsync(userId, commentId, request);
            if (updated)
            {
                return Ok(new { message = "Comment updated successfully" });
            }
            return BadRequest(new { message = "Failed to update comment" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Delete own comment or admin can delete any comment
    /// Note: CommentId is sufficient, slug is used for consistency with route structure
    /// </summary>
    [HttpDelete("{commentId}")]
    [Authorize]
    public async Task<ActionResult> DeleteComment(string slug, Guid commentId)
    {
        try
        {
            var userId = GetUserId();
            var isAdmin = IsAdmin();
            var deleted = await _commentService.DeleteCommentAsync(userId, commentId, isAdmin);
            if (deleted)
            {
                return Ok(new { message = "Comment deleted successfully" });
            }
            return BadRequest(new { message = "Failed to delete comment" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }
}

