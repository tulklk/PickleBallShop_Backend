using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PickleBall.Application.DTOs.Categories;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;
using PickleBall.Infrastructure.Helpers;

namespace PickleBall.Infrastructure.Services;

public class CategoryService : ICategoryService
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public CategoryService(ApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    private static readonly TimeSpan CategoryCacheTtl = TimeSpan.FromMinutes(5);

    private string CacheKeyAll => "categories:all:v1";
    private string CacheKeyTree => "categories:tree:v1";

    public async Task<List<CategoryDto>> GetAllCategoriesAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeyAll, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CategoryCacheTtl;
            return await LoadAllCategoriesAsync();
        });
    }

    public async Task<List<CategoryDto>> GetCategoryTreeAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeyTree, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CategoryCacheTtl;
            return await LoadCategoryTreeAsync();
        });
    }

    private async Task<Dictionary<Guid, int>> GetActiveProductCountsByCategoryAsync()
    {
        return await _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .GroupBy(p => p.CategoryId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }

    private async Task<List<CategoryDto>> LoadAllCategoriesAsync()
    {
        var allCategories = await _context.ProductCategories
            .AsNoTracking()
            .ToListAsync();

        var counts = await GetActiveProductCountsByCategoryAsync();

        var dtoDict = allCategories.ToDictionary(
            c => c.Id,
            c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                ProductCount = counts.GetValueOrDefault(c.Id),
                Children = new List<CategoryDto>()
            });

        foreach (var root in allCategories.Where(c => c.ParentId == null))
            BuildCategoryChildren(dtoDict[root.Id], allCategories, dtoDict);

        return allCategories.Select(c => dtoDict[c.Id]).ToList();
    }

    private async Task<List<CategoryDto>> LoadCategoryTreeAsync()
    {
        var allCategories = await _context.ProductCategories
            .AsNoTracking()
            .ToListAsync();

        var counts = await GetActiveProductCountsByCategoryAsync();

        var dtoDict = allCategories.ToDictionary(
            c => c.Id,
            c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                ProductCount = counts.GetValueOrDefault(c.Id),
                Children = new List<CategoryDto>()
            });

        foreach (var root in allCategories.Where(c => c.ParentId == null))
            BuildCategoryChildren(dtoDict[root.Id], allCategories, dtoDict);

        return allCategories
            .Where(c => c.ParentId == null)
            .Select(c => dtoDict[c.Id])
            .OrderBy(c => c.Name)
            .ToList();
    }

    private static void BuildCategoryChildren(CategoryDto dto, List<ProductCategory> allCategories, Dictionary<Guid, CategoryDto> dtoDict)
    {
        var children = allCategories
            .Where(c => c.ParentId == dto.Id)
            .OrderBy(c => c.Name)
            .ToList();

        foreach (var child in children)
        {
            var childDto = dtoDict[child.Id];
            dto.Children.Add(childDto);
            BuildCategoryChildren(childDto, allCategories, dtoDict);
        }
    }

    public async Task<CategoryDto?> GetCategoryByIdAsync(Guid id)
    {
        var allCategories = await _context.ProductCategories
            .AsNoTracking()
            .ToListAsync();

        var category = allCategories.FirstOrDefault(c => c.Id == id);
        if (category == null)
            return null;

        var counts = await GetActiveProductCountsByCategoryAsync();

        var dtoDict = allCategories.ToDictionary(
            c => c.Id,
            c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                ProductCount = counts.GetValueOrDefault(c.Id),
                Children = new List<CategoryDto>()
            });

        BuildCategoryChildren(dtoDict[id], allCategories, dtoDict);
        return dtoDict[id];
    }

    public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request)
    {
        var parentSlug = await SlugHelper.GenerateUniqueSlugAsync(
            SlugHelper.GenerateSlug(request.Name),
            s => _context.ProductCategories.AnyAsync(c => c.Slug == s));

        var category = new ProductCategory
        {
            Name = request.Name,
            Slug = parentSlug,
            Description = request.Description,
            ParentId = null
        };

        _context.ProductCategories.Add(category);
        await _context.SaveChangesAsync();

        if (request.Children != null && request.Children.Any())
        {
            var childCategories = new List<ProductCategory>();
            foreach (var childRequest in request.Children)
            {
                var childSlug = await SlugHelper.GenerateUniqueSlugAsync(
                    SlugHelper.GenerateSlug(childRequest.Name),
                    s => _context.ProductCategories.AnyAsync(c => c.Slug == s));
                childCategories.Add(new ProductCategory
                {
                    Name = childRequest.Name,
                    Slug = childSlug,
                    Description = childRequest.Description,
                    ParentId = category.Id
                });
            }

            _context.ProductCategories.AddRange(childCategories);
            await _context.SaveChangesAsync();
        }

        InvalidateCategoryCache();
        return (await GetCategoryByIdAsync(category.Id))!;
    }

    public async Task<List<CategoryDto>> CreateCategoriesBulkAsync(List<CreateCategoryRequest> requests)
    {
        var createdCategories = new List<CategoryDto>();

        foreach (var request in requests)
        {
            try
            {
                var category = await CreateCategoryAsync(request);
                createdCategories.Add(category);
            }
            catch
            {
                // Skip if error
            }
        }

        return createdCategories;
    }

    public async Task<CategoryDto> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request)
    {
        var category = await _context.ProductCategories
            .Include(c => c.Children)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
        {
            throw new KeyNotFoundException("Category not found");
        }

        category.Name = request.Name;
        category.Description = request.Description;
        category.Slug = await SlugHelper.GenerateUniqueSlugAsync(
            SlugHelper.GenerateSlug(request.Name),
            s => _context.ProductCategories.AnyAsync(c => c.Slug == s && c.Id != id));

        if (request.Children != null)
        {
            if (category.Children.Any())
            {
                foreach (var child in category.Children)
                {
                    if (await _context.Products.AnyAsync(p => p.CategoryId == child.Id && p.IsActive))
                    {
                        throw new InvalidOperationException($"Cannot remove child category '{child.Name}' because it has active products");
                    }
                }
                _context.ProductCategories.RemoveRange(category.Children);
            }

            if (request.Children.Any())
            {
                var childCategories = new List<ProductCategory>();
                foreach (var childRequest in request.Children)
                {
                    var childSlug = await SlugHelper.GenerateUniqueSlugAsync(
                        SlugHelper.GenerateSlug(childRequest.Name),
                        s => _context.ProductCategories.AnyAsync(c => c.Slug == s));
                    childCategories.Add(new ProductCategory
                    {
                        Name = childRequest.Name,
                        Slug = childSlug,
                        Description = childRequest.Description,
                        ParentId = category.Id
                    });
                }
                _context.ProductCategories.AddRange(childCategories);
            }
        }

        await _context.SaveChangesAsync();

        InvalidateCategoryCache();
        return (await GetCategoryByIdAsync(category.Id))!;
    }

    public async Task<bool> DeleteCategoryAsync(Guid id)
    {
        var category = await _context.ProductCategories
            .Include(c => c.Children)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
        {
            return false;
        }

        if (await _context.Products.AnyAsync(p => p.CategoryId == id && p.IsActive))
        {
            throw new InvalidOperationException("Cannot delete category with active products");
        }

        if (category.Children.Any())
        {
            throw new InvalidOperationException("Cannot delete category that has child categories");
        }

        _context.ProductCategories.Remove(category);
        await _context.SaveChangesAsync();

        InvalidateCategoryCache();
        return true;
    }

    private void InvalidateCategoryCache()
    {
        _cache.Remove(CacheKeyAll);
        _cache.Remove(CacheKeyTree);
    }
}
