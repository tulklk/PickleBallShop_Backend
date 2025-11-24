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
            .Include(c => c.ParentCategory)
            .Include(c => c.Products)
            .Include(c => c.SubCategories)
            .Where(c => c.ParentCategoryId == null)
            .ToListAsync();

        return _mapper.Map<List<CategoryDto>>(categories);
    }

    public async Task<CategoryDto?> GetCategoryByIdAsync(Guid id)
    {
        var category = await _context.ProductCategories
            .Include(c => c.ParentCategory)
            .Include(c => c.Products)
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync(c => c.Id == id);

        return category != null ? _mapper.Map<CategoryDto>(category) : null;
    }

    public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request)
    {
        // Validate parent category if provided
        if (request.ParentCategoryId.HasValue)
        {
            var parentCategory = await _context.ProductCategories.FindAsync(request.ParentCategoryId.Value);
            if (parentCategory == null)
            {
                throw new KeyNotFoundException("Parent category not found");
            }
        }

        var category = new ProductCategory
        {
            Name = request.Name,
            Description = request.Description,
            ParentCategoryId = request.ParentCategoryId
        };

        _context.ProductCategories.Add(category);
        await _context.SaveChangesAsync();

        // Reload with related data
        var createdCategory = await _context.ProductCategories
            .Include(c => c.ParentCategory)
            .Include(c => c.Products)
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync(c => c.Id == category.Id);

        return _mapper.Map<CategoryDto>(createdCategory!);
    }

    public async Task<List<CategoryDto>> CreateCategoriesBulkAsync(List<CreateCategoryRequest> requests)
    {
        var createdCategories = new List<CategoryDto>();
        var categoryIndexMap = new Dictionary<int, Guid>(); // Map request index to created category ID

        // Process requests in order
        for (int i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            Guid? parentId = null;

            // Determine parent ID
            if (request.ParentCategoryId.HasValue)
            {
                // Use provided Guid (existing category in DB)
                var parentExists = await _context.ProductCategories
                    .AnyAsync(c => c.Id == request.ParentCategoryId.Value);
                
                if (parentExists)
                {
                    parentId = request.ParentCategoryId.Value;
                }
                else
                {
                    // Parent not found, skip this request
                    continue;
                }
            }
            else if (request.ParentIndex.HasValue)
            {
                // Use index to reference parent in the same request
                if (request.ParentIndex.Value >= 0 && request.ParentIndex.Value < i && categoryIndexMap.ContainsKey(request.ParentIndex.Value))
                {
                    parentId = categoryIndexMap[request.ParentIndex.Value];
                }
                else
                {
                    // Invalid index, skip this request
                    continue;
                }
            }
            // else: parentId remains null (root category)

            try
            {
                var createRequest = new CreateCategoryRequest
                {
                    Name = request.Name,
                    Description = request.Description,
                    ParentCategoryId = parentId
                };

                var category = await CreateCategoryAsync(createRequest);
                createdCategories.Add(category);
                categoryIndexMap[i] = category.Id;
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
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
        {
            throw new KeyNotFoundException("Category not found");
        }

        // Prevent circular reference - category cannot be its own parent
        if (request.ParentCategoryId.HasValue && request.ParentCategoryId.Value == id)
        {
            throw new InvalidOperationException("Category cannot be its own parent");
        }

        // Check if trying to set parent to one of its subcategories (circular reference)
        if (request.ParentCategoryId.HasValue)
        {
            var isDescendant = await IsDescendantCategoryAsync(id, request.ParentCategoryId.Value);
            if (isDescendant)
            {
                throw new InvalidOperationException("Cannot set parent category to a descendant category");
            }

            var parentCategory = await _context.ProductCategories.FindAsync(request.ParentCategoryId.Value);
            if (parentCategory == null)
            {
                throw new KeyNotFoundException("Parent category not found");
            }
        }

        // Update category properties
        category.Name = request.Name;
        category.Description = request.Description;
        category.ParentCategoryId = request.ParentCategoryId;

        await _context.SaveChangesAsync();

        // Reload with related data
        var updatedCategory = await _context.ProductCategories
            .Include(c => c.ParentCategory)
            .Include(c => c.Products)
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync(c => c.Id == category.Id);

        return _mapper.Map<CategoryDto>(updatedCategory!);
    }

    public async Task<bool> DeleteCategoryAsync(Guid id)
    {
        var category = await _context.ProductCategories
            .Include(c => c.Products)
            .Include(c => c.SubCategories)
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

        // Check if category has subcategories
        if (category.SubCategories.Any())
        {
            throw new InvalidOperationException("Cannot delete category with subcategories. Please delete or move subcategories first");
        }

        _context.ProductCategories.Remove(category);
        await _context.SaveChangesAsync();

        return true;
    }

    private async Task<bool> IsDescendantCategoryAsync(Guid categoryId, Guid potentialAncestorId)
    {
        var category = await _context.ProductCategories
            .Include(c => c.ParentCategory)
            .FirstOrDefaultAsync(c => c.Id == categoryId);

        if (category == null || category.ParentCategoryId == null)
        {
            return false;
        }

        if (category.ParentCategoryId == potentialAncestorId)
        {
            return true;
        }

        return await IsDescendantCategoryAsync(category.ParentCategoryId.Value, potentialAncestorId);
    }
}
