namespace PickleBall.Application.DTOs.Categories;

public class CategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public string? ParentCategoryName { get; set; }
    public List<CategoryDto> SubCategories { get; set; } = new();
    public int ProductCount { get; set; }
}
