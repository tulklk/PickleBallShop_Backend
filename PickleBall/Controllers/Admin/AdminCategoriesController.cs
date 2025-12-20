using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PickleBall.Application.DTOs.Categories;
using PickleBall.Application.Interfaces;

namespace PickleBall.Controllers.Admin;

[ApiController]
[Route("api/admin/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminCategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public AdminCategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetCategories()
    {
        var categories = await _categoryService.GetAllCategoriesAsync();
        return Ok(categories);
    }

    /// <summary>
    /// Get categories with sub-categories grouped (e.g., Vợt Pickleball -> Vợt Pickleball - Adidas)
    /// </summary>
    [HttpGet("tree")]
    public async Task<ActionResult<List<CategoryDto>>> GetCategoryTree()
    {
        var categories = await _categoryService.GetCategoryTreeAsync();
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

    [HttpPost]
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
