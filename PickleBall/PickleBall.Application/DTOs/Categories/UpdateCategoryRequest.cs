using System.ComponentModel.DataAnnotations;

namespace PickleBall.Application.DTOs.Categories;

public class UpdateCategoryRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }

    /// <summary>
    /// Children sub-categories to update. If provided, will replace all existing children. Each child is a full category object with name and description.
    /// </summary>
    public List<CreateCategoryRequest>? Children { get; set; }
}

