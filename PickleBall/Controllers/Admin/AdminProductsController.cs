using AutoMapper;
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
    private readonly IMapper _mapper;

    public AdminProductsController(ApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
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

        // Check if product has variants
        bool hasVariants = request.Variants != null && request.Variants.Count > 0;

        // Validate variants if provided
        if (hasVariants)
        {
            // Validate variant SKUs are unique within the request
            var variantSkus = request.Variants.Select(v => v.SKU).ToList();
            if (variantSkus.Count != variantSkus.Distinct().Count())
            {
                return BadRequest(new { message = "Variant SKUs must be unique within the product" });
            }

            // Validate variant SKUs don't already exist in database
            var existingVariantSkus = await _context.ProductVariants
                .Where(pv => variantSkus.Contains(pv.SKU))
                .Select(pv => pv.SKU)
                .ToListAsync();
            
            if (existingVariantSkus.Any())
            {
                return BadRequest(new { message = $"Variant SKUs already exist: {string.Join(", ", existingVariantSkus)}" });
            }

            // Validate at least one variant attribute (Color, Size, or Spec) is provided for each variant
            foreach (var variant in request.Variants)
            {
                if (string.IsNullOrWhiteSpace(variant.Color) && 
                    string.IsNullOrWhiteSpace(variant.Size) && 
                    string.IsNullOrWhiteSpace(variant.Spec))
                {
                    return BadRequest(new { message = $"Each variant must have at least one attribute (Color, Size, or Spec). SKU: {variant.SKU}" });
                }

                if (string.IsNullOrWhiteSpace(variant.SKU))
                {
                    return BadRequest(new { message = "Variant SKU is required" });
                }
            }
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            CategoryId = request.CategoryId,
            Price = request.Price,
            OriginalPrice = request.OriginalPrice,
            StockQuantity = hasVariants ? 0 : request.StockQuantity, // If has variants, product stock = 0
            SKU = request.SKU,
            Brand = request.Brand,
            IsActive = request.IsActive,
            HasVariants = hasVariants,
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

        // Handle variants if provided
        if (hasVariants && request.Variants != null)
        {
            var variants = request.Variants.Select(v => new ProductVariant
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                Color = v.Color,
                Size = v.Size,
                Spec = v.Spec,
                Price = v.Price,
                OriginalPrice = v.OriginalPrice,
                StockQuantity = v.StockQuantity,
                SKU = v.SKU,
                IsActive = v.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();

        _context.ProductVariants.AddRange(variants);
        await _context.SaveChangesAsync();
    }
    
    // Load product with all necessary includes for mapping
    var createdProduct = await _context.Products
        .Include(p => p.Category)
        .Include(p => p.Images)
        .Include(p => p.Variants.Where(v => v.IsActive))
        .Include(p => p.Reviews)
        .AsNoTracking() // Use AsNoTracking for read-only operation
        .FirstOrDefaultAsync(p => p.Id == product.Id);
    
    if (createdProduct == null)
    {
        return StatusCode(500, new { message = "Failed to retrieve created product" });
    }
    
    // Map to ProductDto to avoid large JSON response and serialization issues
    var productDto = _mapper.Map<ProductDto>(createdProduct);
    
    return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, productDto);
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
            .Include(p => p.Variants)
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

        // Check if variants are provided in request (null = keep existing, not null = replace)
        bool shouldUpdateVariants = request.Variants != null;
        bool hasVariants = request.Variants != null && request.Variants.Count > 0;

        // Validate variants if provided
        if (shouldUpdateVariants && hasVariants)
        {
            // Validate variant SKUs are unique within the request
            var variantSkus = request.Variants.Select(v => v.SKU).ToList();
            if (variantSkus.Count != variantSkus.Distinct().Count())
            {
                return BadRequest(new { message = "Variant SKUs must be unique within the product" });
            }

            // Get existing variant SKUs (excluding current product's variants)
            var existingVariantSkus = await _context.ProductVariants
                .Where(pv => variantSkus.Contains(pv.SKU) && pv.ProductId != id)
                .Select(pv => pv.SKU)
                .ToListAsync();
            
            if (existingVariantSkus.Any())
            {
                return BadRequest(new { message = $"Variant SKUs already exist for other products: {string.Join(", ", existingVariantSkus)}" });
            }

            // Validate at least one variant attribute (Color, Size, or Spec) is provided for each variant
            foreach (var variant in request.Variants)
            {
                if (string.IsNullOrWhiteSpace(variant.Color) && 
                    string.IsNullOrWhiteSpace(variant.Size) && 
                    string.IsNullOrWhiteSpace(variant.Spec))
                {
                    return BadRequest(new { message = $"Each variant must have at least one attribute (Color, Size, or Spec). SKU: {variant.SKU}" });
                }

                if (string.IsNullOrWhiteSpace(variant.SKU))
                {
                    return BadRequest(new { message = "Variant SKU is required" });
                }
            }
        }

        // Update product properties
        product.Name = request.Name;
        product.Description = request.Description;
        product.CategoryId = request.CategoryId;
        product.Price = request.Price;
        product.OriginalPrice = request.OriginalPrice;
        product.SKU = request.SKU;
        product.Brand = request.Brand;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        // Update HasVariants and StockQuantity based on variants
        if (shouldUpdateVariants)
        {
            // Variants are provided in request - update HasVariants
            product.HasVariants = hasVariants;
            product.StockQuantity = hasVariants ? 0 : request.StockQuantity;
        }
        else
        {
            // Variants are not provided - keep existing HasVariants, update StockQuantity only if no variants
            if (!product.HasVariants)
            {
                product.StockQuantity = request.StockQuantity;
            }
            // If product has variants, keep stock at 0
        }

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

        // Handle variants - only update if variants are provided in request
        if (shouldUpdateVariants)
        {
            // Remove existing variants
            _context.ProductVariants.RemoveRange(product.Variants);

            // Add new variants if provided
            if (hasVariants)
            {
                var variants = request.Variants.Select(v => new ProductVariant
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Color = v.Color,
                    Size = v.Size,
                    Spec = v.Spec,
                    Price = v.Price,
                    OriginalPrice = v.OriginalPrice,
                    StockQuantity = v.StockQuantity,
                    SKU = v.SKU,
                    IsActive = v.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }).ToList();

                _context.ProductVariants.AddRange(variants);
            }
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
    public async Task<ActionResult<ProductDto>> GetProduct(Guid id)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Variants.Where(v => v.IsActive))
            .Include(p => p.Reviews)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return NotFound();
        }

        var productDto = _mapper.Map<ProductDto>(product);
        return productDto;
    }

    private async Task<bool> ProductExists(Guid id)
    {
        return await _context.Products.AnyAsync(e => e.Id == id);
    }
}
