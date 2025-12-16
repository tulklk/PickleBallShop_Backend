namespace PickleBall.Application.DTOs.Categories;

public class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>
    /// Children sub-categories to create along with this category
    /// </summary>
    public List<CreateCategoryRequest>? Children { get; set; }
}

