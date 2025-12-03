using PickleBall.Application.DTOs.Wishlist;

namespace PickleBall.Application.Interfaces;

public interface IWishlistService
{
    Task<WishlistDto> GetWishlistAsync(Guid userId);
    Task<WishlistItemDto> AddToWishlistAsync(Guid userId, Guid productId);
    Task RemoveFromWishlistAsync(Guid userId, Guid productId);
    Task<bool> IsInWishlistAsync(Guid userId, Guid productId);
}

