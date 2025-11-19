using Microsoft.AspNetCore.Mvc;
using PickleBall.Application.DTOs.Categories;
using PickleBall.Application.Interfaces;

namespace PickleBall.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetCategories()
    {
        var categories = await _categoryService.GetAllCategoriesAsync();
        return Ok(categories);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CategoryDto>> GetCategory(Guid id)
    {
        var category = await _categoryService.GetCategoryByIdAsync(id);
        if (category == null)
        {
            return NotFound();
        }
        return Ok(category);
    }

    [HttpGet("slug/{slug}")]
    public async Task<ActionResult<CategoryDto>> GetCategoryBySlug(string slug)
    {
        var category = await _categoryService.GetCategoryBySlugAsync(slug);
        if (category == null)
        {
            return NotFound();
        }
        return Ok(category);
    }
}
