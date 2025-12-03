using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PickleBall.Application.DTOs.Wishlist;
using PickleBall.Application.Interfaces;
using System.Security.Claims;

namespace PickleBall.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WishlistController : ControllerBase
{
    private readonly IWishlistService _wishlistService;

    public WishlistController(IWishlistService wishlistService)
    {
        _wishlistService = wishlistService;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdClaim ?? throw new UnauthorizedAccessException());
    }

    /// <summary>
    /// Lấy danh sách sản phẩm yêu thích của user
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<WishlistDto>> GetWishlist()
    {
        var userId = GetUserId();
        var wishlist = await _wishlistService.GetWishlistAsync(userId);
        return Ok(wishlist);
    }

    /// <summary>
    /// Thêm sản phẩm vào wishlist
    /// </summary>
    [HttpPost("{productId:guid}")]
    public async Task<ActionResult<WishlistItemDto>> AddToWishlist(Guid productId)
    {
        try
        {
            var userId = GetUserId();
            var item = await _wishlistService.AddToWishlistAsync(userId, productId);
            return Ok(item);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Xóa sản phẩm khỏi wishlist
    /// </summary>
    [HttpDelete("{productId:guid}")]
    public async Task<ActionResult> RemoveFromWishlist(Guid productId)
    {
        try
        {
            var userId = GetUserId();
            await _wishlistService.RemoveFromWishlistAsync(userId, productId);
            return Ok(new { message = "Product removed from wishlist successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Kiểm tra sản phẩm có trong wishlist không
    /// </summary>
    [HttpGet("{productId:guid}/check")]
    public async Task<ActionResult<object>> CheckInWishlist(Guid productId)
    {
        var userId = GetUserId();
        var isInWishlist = await _wishlistService.IsInWishlistAsync(userId, productId);
        return Ok(new { isInWishlist });
    }
}

