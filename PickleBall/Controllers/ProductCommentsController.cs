using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PickleBall.Application.DTOs.Comments;
using PickleBall.Application.Interfaces;
using System.Security.Claims;

namespace PickleBall.Controllers;

[ApiController]
[Route("api/products/{productId}/comments")]
public class ProductCommentsController : ControllerBase
{
    private readonly IProductCommentService _commentService;

    public ProductCommentsController(IProductCommentService commentService)
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
    /// Get all comments for a product by ID (public endpoint)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<CommentsResponseDto>> GetProductComments(Guid productId)
    {
        try
        {
            var result = await _commentService.GetProductCommentsAsync(productId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Create a new comment (requires authentication)
    /// </summary>
    [EnableRateLimiting("write")]
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<CommentDto>> CreateComment(Guid productId, [FromBody] CreateCommentRequest request)
    {
        try
        {
            var userId = GetUserId();
            var comment = await _commentService.CreateCommentAsync(userId, productId, request);
            return CreatedAtAction(nameof(GetProductComments), new { productId }, comment);
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
    /// Reply to a comment (requires authentication - Any authenticated user can reply)
    /// </summary>
    [EnableRateLimiting("write")]
    [HttpPost("{parentCommentId}/reply")]
    [Authorize]
    public async Task<ActionResult<CommentDto>> ReplyToComment(
        Guid productId, 
        Guid parentCommentId, 
        [FromBody] ReplyCommentRequest request)
    {
        try
        {
            var userId = GetUserId();
            var reply = await _commentService.ReplyToCommentAsync(userId, productId, parentCommentId, request);
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
    /// </summary>
    [HttpPut("{commentId}")]
    [Authorize]
    public async Task<ActionResult> UpdateComment(
        Guid productId, 
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
    /// </summary>
    [HttpDelete("{commentId}")]
    [Authorize]
    public async Task<ActionResult> DeleteComment(Guid productId, Guid commentId)
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

