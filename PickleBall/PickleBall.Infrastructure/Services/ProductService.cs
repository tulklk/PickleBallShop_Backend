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

        if (!string.IsNullOrWhiteSpace(query.ProductType) && Enum.TryParse<ProductType>(query.ProductType, out var productType))
        {
            products = products.Where(p => p.ProductType == productType);
        }

        if (!string.IsNullOrWhiteSpace(query.SkillLevel) && Enum.TryParse<SkillLevel>(query.SkillLevel, out var skillLevel))
        {
            products = products.Where(p => p.SkillLevel == skillLevel);
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

    public async Task<ProductDto?> GetProductBySlugAsync(string slug)
    {
        var product = await _context.Products
            .Include(p => p.Images)
            .Include(p => p.Reviews)
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsActive);

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
}
