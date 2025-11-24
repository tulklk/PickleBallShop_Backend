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

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> GetProduct(Guid id)
    {
        var product = await _productService.GetProductByIdAsync(id);
        if (product == null)
        {
            return NotFound();
        }
        return Ok(product);
    }

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
            return BadRequest(new { message = "Search term is required" });
        }

        var products = await _productService.SearchProductsAsync(q, page, pageSize);
        return Ok(products);
    }

    // CRUD Operations - Admin Only
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductDto>> CreateProduct([FromBody] CreateProductRequest request)
    {
        try
        {
            var product = await _productService.CreateProductAsync(request);
            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
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
    public async Task<ActionResult<ProductDto>> UpdateProduct(Guid id, [FromBody] UpdateProductRequest request)
    {
        try
        {
            var product = await _productService.UpdateProductAsync(id, request);
            return Ok(product);
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
