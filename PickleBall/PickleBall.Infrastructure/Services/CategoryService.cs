using AutoMapper;
using Microsoft.EntityFrameworkCore;
using PickleBall.Application.DTOs.Categories;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;
using PickleBall.Infrastructure.Helpers;

namespace PickleBall.Infrastructure.Services;

public class CategoryService : ICategoryService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public CategoryService(ApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<List<CategoryDto>> GetAllCategoriesAsync()
    {
        var categories = await _context.ProductCategories
            .Include(c => c.Products)
            .Include(c => c.Children)
                .ThenInclude(child => child.Products)
            .ToListAsync();

        var dtoList = _mapper.Map<List<CategoryDto>>(categories);
        
        // Map children manually to avoid cycles
        foreach (var category in categories)
        {
            var dto = dtoList.FirstOrDefault(d => d.Id == category.Id);
            if (dto != null && category.Children.Any())
            {
                dto.Children = _mapper.Map<List<CategoryDto>>(category.Children);
            }
        }

        return dtoList;
    }

    public async Task<List<CategoryDto>> GetCategoryTreeAsync()
    {
        var categories = await _context.ProductCategories
            .Include(c => c.Products)
            .Include(c => c.Children)
                .ThenInclude(child => child.Products)
            .ToListAsync();

        var dtoDict = categories.ToDictionary(
            c => c.Id,
            c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                ProductCount = c.Products.Count(p => p.IsActive),
                Children = new()
            });

        // Build parent-child relationships recursively
        foreach (var category in categories.Where(c => c.ParentId == null))
        {
            MapChildrenRecursively(category, dtoDict[category.Id], categories, dtoDict);
        }

        // Return only root categories (those without parent)
        return dtoDict.Values
            .Where(dto => categories.Any(c => c.Id == dto.Id && c.ParentId == null))
            .OrderBy(c => c.Name)
            .ToList();
    }

    private void MapChildrenRecursively(ProductCategory category, CategoryDto dto, List<ProductCategory> allCategories, Dictionary<Guid, CategoryDto> dtoDict)
    {
        var children = allCategories.Where(c => c.ParentId == category.Id).ToList();
        
        foreach (var child in children)
        {
            var childDto = dtoDict[child.Id];
            dto.Children.Add(childDto);
            // Recursively map children of this child
            MapChildrenRecursively(child, childDto, allCategories, dtoDict);
        }
    }

    public async Task<CategoryDto?> GetCategoryByIdAsync(Guid id)
    {
        var category = await _context.ProductCategories
            .Include(c => c.Products)
            .Include(c => c.Children)
                .ThenInclude(child => child.Products)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
            return null;

        var dto = _mapper.Map<CategoryDto>(category);
        
        // Map children manually
        if (category.Children.Any())
        {
            dto.Children = _mapper.Map<List<CategoryDto>>(category.Children);
        }

        return dto;
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

        // Reload with related data
        var createdCategory = await _context.ProductCategories
            .Include(c => c.Products)
            .Include(c => c.Children)
                .ThenInclude(child => child.Products)
            .FirstOrDefaultAsync(c => c.Id == category.Id);

        var dto = _mapper.Map<CategoryDto>(createdCategory!);
        
        // Map children manually
        if (createdCategory!.Children.Any())
        {
            dto.Children = _mapper.Map<List<CategoryDto>>(createdCategory.Children);
        }

        return dto;
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
                .ThenInclude(child => child.Products)
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
                    if (child.Products.Any(p => p.IsActive))
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

        // Reload with related data
        var updatedCategory = await _context.ProductCategories
            .Include(c => c.Products)
            .Include(c => c.Children)
                .ThenInclude(child => child.Products)
            .FirstOrDefaultAsync(c => c.Id == category.Id);

        var dto = _mapper.Map<CategoryDto>(updatedCategory!);
        
        // Map children manually
        if (updatedCategory!.Children.Any())
        {
            dto.Children = _mapper.Map<List<CategoryDto>>(updatedCategory.Children);
        }

        return dto;
    }

    public async Task<bool> DeleteCategoryAsync(Guid id)
    {
        var category = await _context.ProductCategories
            .Include(c => c.Products)
            .Include(c => c.Children)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
        {
            return false;
        }

        // Check if category has products
        if (category.Products.Any(p => p.IsActive))
        {
            throw new InvalidOperationException("Cannot delete category with active products");
        }

        // Prevent deleting category with children
        if (category.Children.Any())
        {
            throw new InvalidOperationException("Cannot delete category that has child categories");
        }

        _context.ProductCategories.Remove(category);
        await _context.SaveChangesAsync();

        return true;
    }
}
