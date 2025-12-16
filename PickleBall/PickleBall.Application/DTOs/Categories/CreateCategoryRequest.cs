using System.ComponentModel.DataAnnotations;

namespace PickleBall.Application.DTOs.Categories;

/// <summary>
/// Request to create a category. Can include nested children categories.
/// Example: { "name": "Vợt Pickleball", "description": "...", "children": [ { "name": "Vợt Pickleball - Adidas", "description": "..." } ] }
/// </summary>
public class CreateCategoryRequest
{
    /// <summary>
    /// Category name (required)
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Category description (optional)
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Children sub-categories. Each child must be a full CreateCategoryRequest object with name and description properties.
    /// Example: [ { "name": "Child Category", "description": "Child description" } ]
    /// </summary>
    public List<CreateCategoryRequest>? Children { get; set; }
}

