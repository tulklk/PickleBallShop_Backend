using PickleBall.Application.DTOs.Categories;

namespace PickleBall.Application.Interfaces;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllCategoriesAsync();
    Task<CategoryDto?> GetCategoryByIdAsync(Guid id);
    Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request);
    Task<List<CategoryDto>> CreateCategoriesBulkAsync(List<CreateCategoryRequest> requests);
    Task<CategoryDto> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request);
    Task<bool> DeleteCategoryAsync(Guid id);
}
