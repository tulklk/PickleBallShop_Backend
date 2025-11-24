using AutoMapper;
using Microsoft.EntityFrameworkCore;
using PickleBall.Application.DTOs.Products;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;

namespace PickleBall.Infrastructure.Services;

public class ProductService : IProductService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public ProductService(ApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<PagedResultDto<ProductListDto>> GetProductsAsync(ProductQueryDto query)
    {
        var products = _context.Products
            .Include(p => p.Images)
            .Include(p => p.Reviews)
            .Include(p => p.Category)
            .Where(p => p.IsActive)
            .AsQueryable();

        // Apply filters
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

        // Apply sorting
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
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResultDto<ProductListDto>
        {
            Items = _mapper.Map<List<ProductListDto>>(items),
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<ProductDto?> GetProductByIdAsync(Guid id)
    {
        var product = await _context.Products
            .Include(p => p.Images)
            .Include(p => p.Reviews)
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

        return product != null ? _mapper.Map<ProductDto>(product) : null;
    }

    public async Task<List<ProductListDto>> GetFeaturedProductsAsync(int count = 10)
    {
        var products = await _context.Products
            .Include(p => p.Images)
            .Include(p => p.Reviews)
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.Reviews.Count)
            .ThenByDescending(p => p.CreatedAt)
            .Take(count)
            .ToListAsync();

        return _mapper.Map<List<ProductListDto>>(products);
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
        // Check if SKU already exists
        var existingSku = await _context.Products.FirstOrDefaultAsync(p => p.SKU == request.SKU);
        if (existingSku != null)
        {
            throw new InvalidOperationException($"Product with SKU '{request.SKU}' already exists");
        }

        var product = new Product
        {
            Name = request.Name,
            Description = request.Description,
            CategoryId = request.CategoryId,
            Price = request.Price,
            OriginalPrice = request.OriginalPrice,
            StockQuantity = request.StockQuantity,
            SKU = request.SKU,
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

        // Reload with related data
        var createdProduct = await _context.Products
            .Include(p => p.Images)
            .Include(p => p.Reviews)
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == product.Id);

        return _mapper.Map<ProductDto>(createdProduct!);
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

        // Check if SKU already exists (excluding current product)
        var existingSku = await _context.Products.FirstOrDefaultAsync(p => p.SKU == request.SKU && p.Id != id);
        if (existingSku != null)
        {
            throw new InvalidOperationException($"Product with SKU '{request.SKU}' already exists");
        }

        // Update product properties
        product.Name = request.Name;
        product.Description = request.Description;
        product.CategoryId = request.CategoryId;
        product.Price = request.Price;
        product.OriginalPrice = request.OriginalPrice;
        product.StockQuantity = request.StockQuantity;
        product.SKU = request.SKU;
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

        // Reload with related data
        var updatedProduct = await _context.Products
            .Include(p => p.Images)
            .Include(p => p.Reviews)
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == product.Id);

        return _mapper.Map<ProductDto>(updatedProduct!);
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

        return true;
    }
}
