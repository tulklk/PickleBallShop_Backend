using PickleBall.Application.DTOs.Categories;

namespace PickleBall.Application.Interfaces;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllCategoriesAsync();
    Task<CategoryDto?> GetCategoryByIdAsync(Guid id);
    Task<CategoryDto?> GetCategoryBySlugAsync(string slug);
}
