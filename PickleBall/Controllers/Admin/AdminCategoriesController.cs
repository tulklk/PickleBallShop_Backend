using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;

namespace PickleBall.Controllers.Admin;

[ApiController]
[Route("api/admin/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminCategoriesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminCategoriesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<ActionResult<ProductCategory>> CreateCategory([FromBody] ProductCategory category)
    {
        _context.ProductCategories.Add(category);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] ProductCategory category)
    {
        if (id != category.Id)
        {
            return BadRequest();
        }

        _context.Entry(category).State = Microsoft.EntityFrameworkCore.EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await CategoryExists(id))
            {
                return NotFound();
            }
            throw;
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id)
    {
        var category = await _context.ProductCategories.FindAsync(id);
        if (category == null)
        {
            return NotFound();
        }

        _context.ProductCategories.Remove(category);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductCategory>> GetCategory(Guid id)
    {
        var category = await _context.ProductCategories
            .Include(c => c.ParentCategory)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
        {
            return NotFound();
        }

        return category;
    }

    private async Task<bool> CategoryExists(Guid id)
    {
        return await _context.ProductCategories.AnyAsync(e => e.Id == id);
    }
}
