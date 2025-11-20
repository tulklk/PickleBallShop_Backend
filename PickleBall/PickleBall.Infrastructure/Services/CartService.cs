using AutoMapper;
using Microsoft.EntityFrameworkCore;
using PickleBall.Application.DTOs.Cart;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;

namespace PickleBall.Infrastructure.Services;

public class CartService : ICartService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public CartService(ApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<CartDto> GetCartAsync(Guid userId)
    {
        var cart = await _context.Carts
            .Include(c => c.Items)
                .ThenInclude(ci => ci.Product)
                    .ThenInclude(p => p.Images)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null)
        {
            cart = new Cart { UserId = userId };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();
        }

        var cartDto = _mapper.Map<CartDto>(cart);
        cartDto.SubTotal = cart.Items.Sum(ci => ci.UnitPriceSnapshot * ci.Quantity);
        cartDto.TotalItems = cart.Items.Sum(ci => ci.Quantity);

        return cartDto;
    }

    public async Task<CartItemDto> AddCartItemAsync(Guid userId, AddCartItemRequest request)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId && p.IsActive);
        if (product == null)
        {
            throw new KeyNotFoundException("Product not found");
        }

        if (product.StockQuantity < request.Quantity)
        {
            throw new InvalidOperationException("Insufficient stock");
        }

        // Get or create cart
        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null)
        {
            cart = new Cart { UserId = userId };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();
            // Reload cart with items to ensure proper tracking
            cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == cart.Id);
        }

        // Check for existing item
        var existingItem = await _context.CartItems
            .FirstOrDefaultAsync(ci => ci.CartId == cart.Id && ci.ProductId == request.ProductId);

        CartItem cartItem;
        if (existingItem != null)
        {
            existingItem.Quantity += request.Quantity;
            if (product.StockQuantity < existingItem.Quantity)
            {
                throw new InvalidOperationException("Insufficient stock");
            }
            cartItem = existingItem;
        }
        else
        {
            cartItem = new CartItem
            {
                CartId = cart.Id,
                ProductId = request.ProductId,
                Quantity = request.Quantity,
                UnitPriceSnapshot = product.Price
            };
            _context.CartItems.Add(cartItem);
        }

        await _context.SaveChangesAsync();

        // Reload the item with related data
        var updatedItem = await _context.CartItems
            .Include(ci => ci.Product)
                .ThenInclude(p => p.Images)
            .FirstOrDefaultAsync(ci => ci.Id == cartItem.Id);

        return _mapper.Map<CartItemDto>(updatedItem!);
    }

    public async Task<CartItemDto> UpdateCartItemAsync(Guid userId, Guid cartItemId, UpdateCartItemRequest request)
    {
        var cartItem = await _context.CartItems
            .Include(ci => ci.Cart)
            .Include(ci => ci.Product)
                .ThenInclude(p => p.Images)
            .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.Cart.UserId == userId);

        if (cartItem == null)
        {
            throw new KeyNotFoundException("Cart item not found");
        }

        if (request.Quantity <= 0)
        {
            throw new InvalidOperationException("Quantity must be greater than 0");
        }

        if (cartItem.Product.StockQuantity < request.Quantity)
        {
            throw new InvalidOperationException("Insufficient stock");
        }

        cartItem.Quantity = request.Quantity;
        await _context.SaveChangesAsync();

        return _mapper.Map<CartItemDto>(cartItem);
    }

    public async Task RemoveCartItemAsync(Guid userId, Guid cartItemId)
    {
        var cartItem = await _context.CartItems
            .Include(ci => ci.Cart)
            .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.Cart.UserId == userId);

        if (cartItem == null)
        {
            throw new KeyNotFoundException("Cart item not found");
        }

        _context.CartItems.Remove(cartItem);
        await _context.SaveChangesAsync();
    }

    public async Task ClearCartAsync(Guid userId)
    {
        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart != null)
        {
            _context.CartItems.RemoveRange(cart.Items);
            await _context.SaveChangesAsync();
        }
    }
}
