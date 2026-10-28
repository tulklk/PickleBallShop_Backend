using PickleBall.Application.Mapping;
using Microsoft.EntityFrameworkCore;
using PickleBall.Application.DTOs.Wishlist;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;

namespace PickleBall.Infrastructure.Services;

public class WishlistService : IWishlistService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public WishlistService(ApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<WishlistDto> GetWishlistAsync(Guid userId)
    {
        var wishlistItems = await _context.Wishlists
            .Include(w => w.Product)
                .ThenInclude(p => p.Images)
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();

        var items = wishlistItems.Select(w => _mapper.Map<WishlistItemDto>(w)).ToList();

        return new WishlistDto
        {
            Items = items,
            TotalItems = items.Count
        };
    }

    public async Task<WishlistItemDto> AddToWishlistAsync(Guid userId, Guid productId)
    {
        // Check if product exists and is active
        var product = await _context.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);

        if (product == null)
        {
            throw new KeyNotFoundException("Product not found or is not active");
        }

        // Check if item already exists in wishlist
        var existingWishlist = await _context.Wishlists
            .Include(w => w.Product)
                .ThenInclude(p => p.Images)
            .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId);

        if (existingWishlist != null)
        {
            // Return existing item
            return _mapper.Map<WishlistItemDto>(existingWishlist);
        }

        // Create new wishlist item
        var wishlist = new Wishlist
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProductId = productId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Wishlists.Add(wishlist);
        await _context.SaveChangesAsync();

        // Reload with product data
        var wishlistWithProduct = await _context.Wishlists
            .Include(w => w.Product)
                .ThenInclude(p => p.Images)
            .FirstOrDefaultAsync(w => w.Id == wishlist.Id);

        return _mapper.Map<WishlistItemDto>(wishlistWithProduct!);
    }

    public async Task RemoveFromWishlistAsync(Guid userId, Guid productId)
    {
        var wishlist = await _context.Wishlists
            .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId);

        if (wishlist == null)
        {
            throw new KeyNotFoundException("Product not found in wishlist");
        }

        _context.Wishlists.Remove(wishlist);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> IsInWishlistAsync(Guid userId, Guid productId)
    {
        return await _context.Wishlists
            .AnyAsync(w => w.UserId == userId && w.ProductId == productId);
    }
}

