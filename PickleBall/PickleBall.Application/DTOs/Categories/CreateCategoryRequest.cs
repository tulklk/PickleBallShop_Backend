using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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
    /// Children sub-categories. Accepts either an array of strings (child names) or an array of objects with name and description.
    /// Example: [ "Vợt Pickleball Head", "Vợt Pickleball Joola" ] or [ { "name": "Child Category", "description": "Child description" } ]
    /// </summary>
    [JsonConverter(typeof(CreateCategoryRequestChildrenConverter))]
    public List<CreateCategoryRequest>? Children { get; set; }
}

