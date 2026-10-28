using PickleBall.Application.Mapping;
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
            .Include(c => c.Items)
                .ThenInclude(ci => ci.ProductVariant)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null)
        {
            cart = new Cart { UserId = userId };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();
        }

        // Keep displayed prices identical to what checkout will charge (checkout always re-prices from the catalog).
        var priceChanged = false;
        foreach (var item in cart.Items)
        {
            var current = item.ProductVariant?.Price ?? item.Product.Price;
            if (item.UnitPriceSnapshot != current)
            {
                item.UnitPriceSnapshot = current;
                priceChanged = true;
            }
        }
        if (priceChanged)
        {
            await _context.SaveChangesAsync();
        }

        var cartDto = _mapper.Map<CartDto>(cart);
        cartDto.SubTotal = cart.Items.Sum(ci => ci.UnitPriceSnapshot * ci.Quantity);
        cartDto.TotalItems = cart.Items.Sum(ci => ci.Quantity);

        return cartDto;
    }

    public async Task<CartItemDto> AddCartItemAsync(Guid userId, AddCartItemRequest request)
    {
        // Load product with variants
        var product = await _context.Products
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.IsActive);
        
        if (product == null)
        {
            throw new KeyNotFoundException("Product not found");
        }

        // Validate variant selection
        ProductVariant? selectedVariant = null;
        if (product.HasVariants)
        {
            // Product has variants, must provide ProductVariantId
            if (!request.ProductVariantId.HasValue)
            {
                throw new InvalidOperationException("Product has variants. Please select a variant (color, size, or spec).");
            }

            selectedVariant = await _context.ProductVariants
                .FirstOrDefaultAsync(v => v.Id == request.ProductVariantId.Value && v.ProductId == product.Id && v.IsActive);
            
            if (selectedVariant == null)
            {
                throw new KeyNotFoundException("Selected variant not found or is not active");
            }

            // Check stock at variant level
            if (selectedVariant.StockQuantity < request.Quantity)
            {
                throw new InvalidOperationException($"Insufficient stock for selected variant. Available: {selectedVariant.StockQuantity}");
            }
        }
        else
        {
            // Product doesn't have variants, ProductVariantId should be null
            if (request.ProductVariantId.HasValue)
            {
                throw new InvalidOperationException("This product does not have variants. ProductVariantId should be null.");
            }

            // Check stock at product level
            if (product.StockQuantity < request.Quantity)
            {
                throw new InvalidOperationException($"Insufficient stock. Available: {product.StockQuantity}");
            }
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

        // Check for existing item (same product AND same variant)
        var existingItem = await _context.CartItems
            .FirstOrDefaultAsync(ci => ci.CartId == cart.Id 
                && ci.ProductId == request.ProductId 
                && ci.ProductVariantId == request.ProductVariantId);

        CartItem cartItem;
        decimal unitPrice = selectedVariant?.Price ?? product.Price;
        
        if (existingItem != null)
        {
            existingItem.Quantity += request.Quantity;
            existingItem.UnitPriceSnapshot = unitPrice;
            if (existingItem.Quantity > 100)
            {
                throw new InvalidOperationException("Quantity must not exceed 100 per item");
            }
            
            // Re-validate stock
            if (product.HasVariants && selectedVariant != null)
            {
                if (selectedVariant.StockQuantity < existingItem.Quantity)
                {
                    throw new InvalidOperationException($"Insufficient stock for selected variant. Available: {selectedVariant.StockQuantity}, Requested: {existingItem.Quantity}");
                }
            }
            else
            {
                if (product.StockQuantity < existingItem.Quantity)
                {
                    throw new InvalidOperationException($"Insufficient stock. Available: {product.StockQuantity}, Requested: {existingItem.Quantity}");
                }
            }
            
            cartItem = existingItem;
        }
        else
        {
            cartItem = new CartItem
            {
                CartId = cart.Id,
                ProductId = request.ProductId,
                ProductVariantId = request.ProductVariantId,
                Quantity = request.Quantity,
                UnitPriceSnapshot = unitPrice
            };
            _context.CartItems.Add(cartItem);
        }

        await _context.SaveChangesAsync();

        // Reload the item with related data
        var updatedItem = await _context.CartItems
            .Include(ci => ci.Product)
                .ThenInclude(p => p.Images)
            .Include(ci => ci.ProductVariant)
            .FirstOrDefaultAsync(ci => ci.Id == cartItem.Id);

        return _mapper.Map<CartItemDto>(updatedItem!);
    }

    public async Task<CartItemDto> UpdateCartItemAsync(Guid userId, Guid cartItemId, UpdateCartItemRequest request)
    {
        var cartItem = await _context.CartItems
            .Include(ci => ci.Cart)
            .Include(ci => ci.Product)
                .ThenInclude(p => p.Images)
            .Include(ci => ci.ProductVariant)
            .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.Cart.UserId == userId);

        if (cartItem == null)
        {
            throw new KeyNotFoundException("Cart item not found");
        }

        if (request.Quantity <= 0 || request.Quantity > 100)
        {
            throw new InvalidOperationException("Quantity must be between 1 and 100");
        }

        // Check stock based on whether product has variants
        if (cartItem.Product.HasVariants && cartItem.ProductVariant != null)
        {
            // Check stock at variant level
            if (cartItem.ProductVariant.StockQuantity < request.Quantity)
            {
                throw new InvalidOperationException($"Insufficient stock for selected variant. Available: {cartItem.ProductVariant.StockQuantity}");
            }
        }
        else
        {
            // Check stock at product level
            if (cartItem.Product.StockQuantity < request.Quantity)
            {
                throw new InvalidOperationException($"Insufficient stock. Available: {cartItem.Product.StockQuantity}");
            }
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
