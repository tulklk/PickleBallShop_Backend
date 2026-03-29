using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PickleBall.Application.DTOs.Products;
using PickleBall.Application.Interfaces;

namespace PickleBall.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDto<ProductListDto>>> GetProducts([FromQuery] ProductQueryDto query)
    {
        var result = await _productService.GetProductsAsync(query);
        return Ok(result);
    }

    /// <summary>Literal routes must be declared before {idOrSlug} so they are not captured as slugs.</summary>
    [HttpGet("featured")]
    public async Task<ActionResult<List<ProductListDto>>> GetFeaturedProducts([FromQuery] int count = 10)
    {
        var products = await _productService.GetFeaturedProductsAsync(count);
        return Ok(products);
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<ProductListDto>>> SearchProducts([FromQuery] string q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { success = false, message = "Search term is required", traceId = HttpContext.TraceIdentifier });
        }

        var products = await _productService.SearchProductsAsync(q, page, pageSize);
        return Ok(products);
    }

    /// <summary>
    /// Get product by ID or slug (SEO-friendly)
    /// Supports both:
    /// - GET /api/products/{guid} (e.g., /api/products/123e4567-e89b-12d3-a456-426614174000)
    /// - GET /api/products/{slug} (e.g., /api/products/ao-polo-pickleball-performance)
    /// </summary>
    [HttpGet("{idOrSlug}")]
    public async Task<ActionResult<ProductDto>> GetProduct(string idOrSlug)
    {
        if (string.IsNullOrWhiteSpace(idOrSlug))
        {
            return BadRequest(new { success = false, message = "Product ID or slug is required", traceId = HttpContext.TraceIdentifier });
        }

        // Try to parse as Guid first (backward compatibility)
        if (Guid.TryParse(idOrSlug, out var id))
        {
            var productById = await _productService.GetProductByIdAsync(id);
            if (productById != null)
            {
                return Ok(productById);
            }
        }

        // If not a valid Guid or not found by ID, try slug
        var productBySlug = await _productService.GetProductBySlugAsync(idOrSlug);
        if (productBySlug != null)
        {
            return Ok(productBySlug);
        }

        return NotFound(new { success = false, message = "Product not found", traceId = HttpContext.TraceIdentifier });
    }

    // CRUD Operations - Admin Only
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductDto>> CreateProduct([FromBody] CreateProductRequest request)
    {
        try
        {
            var product = await _productService.CreateProductAsync(request);
            return CreatedAtAction(nameof(GetProduct), new { idOrSlug = product.Id }, product);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message, traceId = HttpContext.TraceIdentifier });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message, traceId = HttpContext.TraceIdentifier });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductDto>> UpdateProduct(Guid id, [FromBody] UpdateProductRequest request)
    {
        try
        {
            var product = await _productService.UpdateProductAsync(id, request);
            return Ok(product);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message, traceId = HttpContext.TraceIdentifier });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message, traceId = HttpContext.TraceIdentifier });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteProduct(Guid id)
    {
        var deleted = await _productService.DeleteProductAsync(id);
        if (!deleted)
        {
            return NotFound();
        }
        return NoContent();
    }
}
