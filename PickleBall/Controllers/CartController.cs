using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PickleBall.Application.DTOs.Cart;
using PickleBall.Application.Interfaces;
using System.Security.Claims;

namespace PickleBall.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;

    public CartController(ICartService cartService)
    {
        _cartService = cartService;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdClaim ?? throw new UnauthorizedAccessException());
    }

    [HttpGet]
    public async Task<ActionResult<CartDto>> GetCart()
    {
        var userId = GetUserId();
        var cart = await _cartService.GetCartAsync(userId);
        return Ok(cart);
    }

    [HttpPost("items")]
    public async Task<ActionResult<CartItemDto>> AddItem([FromBody] AddCartItemRequest request)
    {
        try
        {
            var userId = GetUserId();
            var item = await _cartService.AddCartItemAsync(userId, request);
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

    [HttpPut("items/{cartItemId:guid}")]
    public async Task<ActionResult<CartItemDto>> UpdateItem(Guid cartItemId, [FromBody] UpdateCartItemRequest request)
    {
        try
        {
            var userId = GetUserId();
            var item = await _cartService.UpdateCartItemAsync(userId, cartItemId, request);
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

    [HttpDelete("items/{cartItemId:guid}")]
    public async Task<ActionResult> RemoveItem(Guid cartItemId)
    {
        try
        {
            var userId = GetUserId();
            await _cartService.RemoveCartItemAsync(userId, cartItemId);
            return Ok(new { message = "Item removed successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete]
    public async Task<ActionResult> ClearCart()
    {
        var userId = GetUserId();
        await _cartService.ClearCartAsync(userId);
        return Ok(new { message = "Cart cleared successfully" });
    }
}
