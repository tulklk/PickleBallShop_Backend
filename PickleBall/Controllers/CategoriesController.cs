using Microsoft.AspNetCore.Authorization;
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

    // CRUD Operations - Admin Only
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CategoryDto>> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        try
        {
            var category = await _categoryService.CreateCategoryAsync(request);
            return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request)
    {
        try
        {
            var category = await _categoryService.UpdateCategoryAsync(id, request);
            return Ok(category);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteCategory(Guid id)
    {
        try
        {
            var deleted = await _categoryService.DeleteCategoryAsync(id);
            if (!deleted)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("bulk")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<CategoryDto>>> CreateCategoriesBulk([FromBody] List<CreateCategoryRequest> requests)
    {
        if (requests == null || !requests.Any())
        {
            return BadRequest(new { message = "Request list cannot be empty" });
        }

        try
        {
            var categories = await _categoryService.CreateCategoriesBulkAsync(requests);
            return Ok(categories);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
