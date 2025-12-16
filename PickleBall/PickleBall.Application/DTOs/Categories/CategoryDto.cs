namespace PickleBall.Application.DTOs.Categories;

public class CategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ProductCount { get; set; }

    /// <summary>
    /// Children sub-categories (e.g., Vợt Pickleball - Adidas belongs to Vợt Pickleball)
    /// </summary>
    public List<CategoryDto> Children { get; set; } = new();
}
