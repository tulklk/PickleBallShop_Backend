using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PickleBall.Application.DTOs.Products;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;

namespace PickleBall.Controllers.Admin;

[ApiController]
[Route("api/admin/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminProductsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminProductsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<ActionResult<Product>> CreateProduct([FromBody] CreateProductRequest request)
    {
        // Check model state validation (FluentValidation errors)
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Validate category exists
        if (request.CategoryId == Guid.Empty)
        {
            return BadRequest(new { message = "Category ID is required" });
        }

        var categoryExists = await _context.ProductCategories.AnyAsync(c => c.Id == request.CategoryId);
        if (!categoryExists)
        {
            return BadRequest(new { message = "Category not found" });
        }

        // Check if SKU already exists
        var skuExists = await _context.Products.AnyAsync(p => p.SKU == request.SKU);
        if (skuExists)
        {
            return BadRequest(new { message = "SKU already exists" });
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            CategoryId = request.CategoryId,
            Price = request.Price,
            OriginalPrice = request.OriginalPrice,
            StockQuantity = request.StockQuantity,
            SKU = request.SKU,
            Brand = request.Brand,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Handle images
        if (request.ImageUrls != null && request.ImageUrls.Count > 0)
        {
            product.Images = request.ImageUrls.Select((url, index) => new ProductImage
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                ImageUrl = url,
                IsPrimary = index == 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();
        }

        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        
        var createdProduct = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == product.Id);
            
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, createdProduct);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateProductRequest request)
    {
        // Check model state validation (FluentValidation errors)
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var product = await _context.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return NotFound();
        }

        // Validate category exists
        if (request.CategoryId == Guid.Empty)
        {
            return BadRequest(new { message = "Category ID is required" });
        }

        var categoryExists = await _context.ProductCategories.AnyAsync(c => c.Id == request.CategoryId);
        if (!categoryExists)
        {
            return BadRequest(new { message = "Category not found" });
        }

        // Check if SKU already exists (excluding current product)
        var skuExists = await _context.Products.AnyAsync(p => p.SKU == request.SKU && p.Id != id);
        if (skuExists)
        {
            return BadRequest(new { message = "SKU already exists" });
        }

        // Update product properties
        product.Name = request.Name;
        product.Description = request.Description;
        product.CategoryId = request.CategoryId;
        product.Price = request.Price;
        product.OriginalPrice = request.OriginalPrice;
        product.StockQuantity = request.StockQuantity;
        product.SKU = request.SKU;
        product.Brand = request.Brand;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        // Handle images - remove existing and add new ones
        _context.ProductImages.RemoveRange(product.Images);
        
        if (request.ImageUrls != null && request.ImageUrls.Count > 0)
        {
            var newImages = request.ImageUrls.Select((url, index) => new ProductImage
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                ImageUrl = url,
                IsPrimary = index == 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();
            
            _context.ProductImages.AddRange(newImages);
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await ProductExists(id))
            {
                return NotFound();
            }
            throw;
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteProduct(Guid id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
        {
            return NotFound();
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Product>> GetProduct(Guid id)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return NotFound();
        }

        return product;
    }

    private async Task<bool> ProductExists(Guid id)
    {
        return await _context.Products.AnyAsync(e => e.Id == id);
    }
}
