using PickleBall.Application.DTOs.Cart;

namespace PickleBall.Application.Interfaces;

public interface ICartService
{
    Task<CartDto> GetCartAsync(Guid userId);
    Task<CartItemDto> AddCartItemAsync(Guid userId, AddCartItemRequest request);
    Task<CartItemDto> UpdateCartItemAsync(Guid userId, Guid cartItemId, UpdateCartItemRequest request);
    Task RemoveCartItemAsync(Guid userId, Guid cartItemId);
    Task ClearCartAsync(Guid userId);
}
