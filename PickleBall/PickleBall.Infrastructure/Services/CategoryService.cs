using AutoMapper;
using Microsoft.EntityFrameworkCore;
using PickleBall.Application.DTOs.Categories;
using PickleBall.Application.Interfaces;
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

    public async Task<CategoryDto?> GetCategoryBySlugAsync(string slug)
    {
        var category = await _context.ProductCategories
            .Include(c => c.ParentCategory)
            .Include(c => c.Products)
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync(c => c.Slug == slug);

        return category != null ? _mapper.Map<CategoryDto>(category) : null;
    }
}
