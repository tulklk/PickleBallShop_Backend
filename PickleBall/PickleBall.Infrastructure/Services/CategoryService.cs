using AutoMapper;
using Microsoft.EntityFrameworkCore;
using PickleBall.Application.DTOs.Categories;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;

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
            .ToListAsync();

        return _mapper.Map<List<CategoryDto>>(categories);
    }

        public async Task<List<CategoryDto>> GetCategoryTreeAsync()
        {
            var categories = await _context.ProductCategories
                .Include(c => c.Products)
                .ToListAsync();

            // Create DTOs with product count
            var dtoDict = categories.ToDictionary(
                c => c.Name,
                c => new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    ProductCount = c.Products.Count
                });

            // Build parent-child relationship by name prefix: "Parent - Child"
            foreach (var category in categories)
            {
                if (!category.Name.Contains(" - "))
                {
                    continue;
                }

                var parts = category.Name.Split(" - ", 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2) continue;

                var parentName = parts[0];
                var childName = category.Name;

                if (dtoDict.TryGetValue(parentName, out var parentDto) &&
                    dtoDict.TryGetValue(childName, out var childDto))
                {
                    parentDto.Children.Add(childDto);
                }
            }

            // Return only top-level categories (no " - " in name), with children populated
            return dtoDict.Values
                .Where(c => !c.Name.Contains(" - "))
                .OrderBy(c => c.Name)
                .ToList();
        }

    public async Task<CategoryDto?> GetCategoryByIdAsync(Guid id)
    {
        var category = await _context.ProductCategories
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == id);

        return category != null ? _mapper.Map<CategoryDto>(category) : null;
    }

    public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request)
    {
        var category = new ProductCategory
        {
            Name = request.Name,
            Description = request.Description
        };

        _context.ProductCategories.Add(category);
        await _context.SaveChangesAsync();

        // Reload with related data
        var createdCategory = await _context.ProductCategories
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == category.Id);

        return _mapper.Map<CategoryDto>(createdCategory!);
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
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
        {
            throw new KeyNotFoundException("Category not found");
        }

        // Update category properties
        category.Name = request.Name;
        category.Description = request.Description;

        await _context.SaveChangesAsync();

        // Reload with related data
        var updatedCategory = await _context.ProductCategories
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == category.Id);

        return _mapper.Map<CategoryDto>(updatedCategory!);
    }

    public async Task<bool> DeleteCategoryAsync(Guid id)
    {
        var category = await _context.ProductCategories
            .Include(c => c.Products)
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

        _context.ProductCategories.Remove(category);
        await _context.SaveChangesAsync();

        return true;
    }
}
