using System.Linq.Expressions;
using PickleBall.Application.Mapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PickleBall.Application.DTOs.Products;
using PickleBall.Application.Helpers;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;
using PickleBall.Infrastructure.Helpers;

namespace PickleBall.Infrastructure.Services;

public class ProductService : IProductService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan FeaturedCacheTtl = TimeSpan.FromMinutes(5);

    public ProductService(ApplicationDbContext context, IMapper mapper, IMemoryCache cache)
    {
        _context = context;
        _mapper = mapper;
        _cache = cache;
    }

    public async Task<PagedResultDto<ProductListDto>> GetProductsAsync(ProductQueryDto query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        IQueryable<Product> products = _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive);

        if (query.CategoryId.HasValue)
        {
            products = products.Where(p => p.CategoryId == query.CategoryId.Value);
        }

        if (query.MinPrice.HasValue)
        {
            products = products.Where(p => p.Price >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            products = products.Where(p => p.Price <= query.MaxPrice.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Brand))
        {
            products = products.Where(p => p.Brand != null && p.Brand.Contains(query.Brand));
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm.ToLower();
            products = products.Where(p =>
                p.Name.ToLower().Contains(searchTerm) ||
                (p.Description != null && p.Description.ToLower().Contains(searchTerm)) ||
                (p.Brand != null && p.Brand.ToLower().Contains(searchTerm)));
        }

        products = query.SortBy?.ToLower() switch
        {
            "price" => products.OrderBy(p => p.Price),
            "price_desc" => products.OrderByDescending(p => p.Price),
            "newest" => products.OrderByDescending(p => p.CreatedAt),
            "popularity" => products.OrderByDescending(p => p.Reviews.Count),
            _ => products.OrderBy(p => p.Name)
        };

        var totalCount = await products.CountAsync();

        var items = await products
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapProductListProjection())
            .ToListAsync();

        return new PagedResultDto<ProductListDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Projects to list DTO in SQL (no cartesian explosion from Include Reviews/Images).
    /// </summary>
    private static Expression<Func<Product, ProductListDto>> MapProductListProjection()
    {
        return p => new ProductListDto
        {
            Id = p.Id,
            Name = p.Name,
            Slug = p.Slug,
            Price = p.Price,
            OriginalPrice = p.OriginalPrice,
            StockQuantity = p.StockQuantity,
            Brand = p.Brand,
            ReviewCount = p.Reviews.Count,
            AverageRating = p.Reviews.Any() ? p.Reviews.Average(r => (double)r.Rating) : null,
            PrimaryImageUrl = p.Images.Where(i => i.IsPrimary).Select(i => i.ImageUrl).FirstOrDefault()
                ?? p.Images.Select(i => i.ImageUrl).FirstOrDefault()
        };
    }

    public async Task<ProductDto?> GetProductByIdAsync(Guid id)
    {
        var product = await _context.Products
            .AsSplitQuery()
            .AsNoTracking()
            .Include(p => p.Images)
            .Include(p => p.Category)
            .Include(p => p.Variants.Where(v => v.IsActive))
            .Include(p => p.Specifications.OrderBy(s => s.DisplayOrder))
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

        if (product == null) return null;

        var dto = _mapper.Map<ProductDto>(product);
        await ApplyReviewStatsAsync(dto, id);
        return dto;
    }

    public async Task<ProductDto?> GetProductBySlugAsync(string slug)
    {
        var product = await _context.Products
            .AsSplitQuery()
            .AsNoTracking()
            .Include(p => p.Images)
            .Include(p => p.Category)
            .Include(p => p.Variants.Where(v => v.IsActive))
            .Include(p => p.Specifications.OrderBy(s => s.DisplayOrder))
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsActive);

        if (product == null) return null;

        var dto = _mapper.Map<ProductDto>(product);
        await ApplyReviewStatsAsync(dto, product.Id);
        return dto;
    }

    private async Task ApplyReviewStatsAsync(ProductDto dto, Guid productId)
    {
        var count = await _context.ProductReviews.AsNoTracking().CountAsync(r => r.ProductId == productId);
        dto.ReviewCount = count;
        dto.AverageRating = count == 0
            ? null
            : await _context.ProductReviews.AsNoTracking()
                .Where(r => r.ProductId == productId)
                .AverageAsync(r => (double)r.Rating);
    }

    public async Task<List<ProductListDto>> GetFeaturedProductsAsync(int count = 10)
    {
        count = Math.Clamp(count, 1, 100);
        return await _cache.GetOrCreateAsync($"products:featured:{count}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = FeaturedCacheTtl;
            return await _context.Products
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.Reviews.Count)
                .ThenByDescending(p => p.CreatedAt)
                .Take(count)
                .Select(MapProductListProjection())
                .ToListAsync();
        });
    }

    public async Task<List<ProductListDto>> SearchProductsAsync(string searchTerm, int page = 1, int pageSize = 20)
    {
        var query = new ProductQueryDto
        {
            SearchTerm = searchTerm,
            Page = page,
            PageSize = pageSize
        };

        var result = await GetProductsAsync(query);
        return result.Items;
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request)
    {
        // Generate slug from product name
        var baseSlug = SlugHelper.GenerateSlug(request.Name);
        var slug = await SlugHelper.GenerateUniqueSlugAsync(baseSlug, async (s) =>
        {
            return await _context.Products.AnyAsync(p => p.Slug == s);
        });

        ProductPriceHelper.SetEffectivePrices(request.Price, request.OriginalPrice, out var effectivePrice, out var storedOriginalPrice);
        var product = new Product
        {
            Name = request.Name,
            Slug = slug,
            Description = request.Description,
            CategoryId = request.CategoryId,
            Price = effectivePrice,
            OriginalPrice = storedOriginalPrice,
            StockQuantity = request.StockQuantity,
            Brand = request.Brand,
            IsActive = request.IsActive
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Add images
        if (request.ImageUrls.Any())
        {
            var images = request.ImageUrls.Select((url, index) => new ProductImage
            {
                ProductId = product.Id,
                ImageUrl = url,
                IsPrimary = index == 0
            }).ToList();

            _context.ProductImages.AddRange(images);
            await _context.SaveChangesAsync();
        }

        InvalidateFeaturedProductCache();

        // Reload with related data (no Reviews — stats applied separately)
        var createdProduct = await _context.Products
            .AsSplitQuery()
            .AsNoTracking()
            .Include(p => p.Images)
            .Include(p => p.Category)
            .Include(p => p.Variants.Where(v => v.IsActive))
            .Include(p => p.Specifications.OrderBy(s => s.DisplayOrder))
            .FirstOrDefaultAsync(p => p.Id == product.Id);

        var dto = _mapper.Map<ProductDto>(createdProduct!);
        await ApplyReviewStatsAsync(dto, product.Id);
        return dto;
    }

    public async Task<ProductDto> UpdateProductAsync(Guid id, UpdateProductRequest request)
    {
        var product = await _context.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            throw new KeyNotFoundException("Product not found");
        }

        // Generate slug from product name if name changed
        if (product.Name != request.Name)
        {
            var baseSlug = SlugHelper.GenerateSlug(request.Name);
            product.Slug = await SlugHelper.GenerateUniqueSlugAsync(baseSlug, async (s) =>
            {
                return await _context.Products.AnyAsync(p => p.Slug == s && p.Id != id);
            });
        }

        // Update product properties (Giá bán: ưu tiên Giá giảm, không có thì dùng Giá gốc)
        ProductPriceHelper.SetEffectivePrices(request.Price, request.OriginalPrice, out var effectivePrice, out var storedOriginalPrice);
        product.Name = request.Name;
        product.Description = request.Description;
        product.CategoryId = request.CategoryId;
        product.Price = effectivePrice;
        product.OriginalPrice = storedOriginalPrice;
        product.StockQuantity = request.StockQuantity;
        product.Brand = request.Brand;
        product.IsActive = request.IsActive;

        // Update images
        _context.ProductImages.RemoveRange(product.Images);
        if (request.ImageUrls.Any())
        {
            var images = request.ImageUrls.Select((url, index) => new ProductImage
            {
                ProductId = product.Id,
                ImageUrl = url,
                IsPrimary = index == 0
            }).ToList();

            _context.ProductImages.AddRange(images);
        }

        await _context.SaveChangesAsync();

        InvalidateFeaturedProductCache();

        // Reload with related data
        var updatedProduct = await _context.Products
            .AsSplitQuery()
            .AsNoTracking()
            .Include(p => p.Images)
            .Include(p => p.Category)
            .Include(p => p.Variants.Where(v => v.IsActive))
            .Include(p => p.Specifications.OrderBy(s => s.DisplayOrder))
            .FirstOrDefaultAsync(p => p.Id == product.Id);

        var dto = _mapper.Map<ProductDto>(updatedProduct!);
        await ApplyReviewStatsAsync(dto, id);
        return dto;
    }

    public async Task<bool> DeleteProductAsync(Guid id)
    {
        var product = await _context.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return false;
        }

        // Soft delete by setting IsActive to false
        product.IsActive = false;
        await _context.SaveChangesAsync();

        InvalidateFeaturedProductCache();

        return true;
    }

    private void InvalidateFeaturedProductCache()
    {
        for (var i = 1; i <= 100; i++)
            _cache.Remove($"products:featured:{i}");
    }
}
