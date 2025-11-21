namespace PickleBall.Application.DTOs.Categories;

public class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentCategoryId { get; set; }
    /// <summary>
    /// Index of parent category in the same request array (0-based). Used when creating parent and child in the same bulk request.
    /// If both ParentCategoryId and ParentIndex are provided, ParentCategoryId takes precedence.
    /// </summary>
    public int? ParentIndex { get; set; }
}

