using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PickleBall.Application.DTOs.Reviews;
using PickleBall.Application.Interfaces;
using System.Security.Claims;

namespace PickleBall.Controllers;

[ApiController]
[Route("api/products/{productIdOrSlug}/reviews")]
public class ProductReviewsController : ControllerBase
{
    private readonly IProductReviewService _reviewService;

    public ProductReviewsController(IProductReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }
        return userId;
    }

    /// <summary>
    /// Get all reviews for a product by ID or slug (public endpoint)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ReviewsResponseDto>> GetProductReviews(string productIdOrSlug)
    {
        try
        {
            // Try to parse as Guid first
            if (Guid.TryParse(productIdOrSlug, out var productId))
            {
                var result = await _reviewService.GetProductReviewsAsync(productId);
                return Ok(result);
            }

            // If not a valid Guid, try slug
            var resultBySlug = await _reviewService.GetProductReviewsBySlugAsync(productIdOrSlug);
            return Ok(resultBySlug);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Create a new review (public endpoint - no authentication required)
    /// If user is authenticated, their userId will be linked to the review
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ReviewDto>> CreateReview(string productIdOrSlug, [FromBody] CreateReviewRequest request)
    {
        try
        {
            Guid? userId = null;
            
            // If user is authenticated, get their userId (optional)
            if (User.Identity?.IsAuthenticated == true)
            {
                userId = GetUserId();
            }

            ReviewDto review;

            // Try to parse as Guid first
            if (Guid.TryParse(productIdOrSlug, out var productId))
            {
                review = await _reviewService.CreateReviewAsync(userId, productId, request);
            }
            else
            {
                // If not a valid Guid, try slug
                review = await _reviewService.CreateReviewBySlugAsync(userId, productIdOrSlug, request);
            }

            return CreatedAtAction(nameof(GetProductReviews), new { productIdOrSlug }, review);
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
}

