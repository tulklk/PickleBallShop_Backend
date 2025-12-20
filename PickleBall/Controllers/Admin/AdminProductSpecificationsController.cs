using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PickleBall.Application.DTOs.Products;
using PickleBall.Infrastructure.Data;

namespace PickleBall.Controllers.Admin;

[ApiController]
[Route("api/admin/products/{productId:guid}/specifications")]
[Authorize(Roles = "Admin")]
public class AdminProductSpecificationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public AdminProductSpecificationsController(ApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    /// <summary>
    /// Lấy tất cả thông số kỹ thuật của sản phẩm
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ProductSpecificationDto>>> GetSpecifications(Guid productId)
    {
        var specifications = await _context.ProductSpecifications
            .Where(ps => ps.ProductId == productId)
            .OrderBy(ps => ps.DisplayOrder)
            .ToListAsync();

        return Ok(_mapper.Map<List<ProductSpecificationDto>>(specifications));
    }

    /// <summary>
    /// Lấy một thông số kỹ thuật theo ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductSpecificationDto>> GetSpecification(Guid productId, Guid id)
    {
        var specification = await _context.ProductSpecifications
            .FirstOrDefaultAsync(ps => ps.Id == id && ps.ProductId == productId);

        if (specification == null)
        {
            return NotFound(new { message = "Không tìm thấy thông số kỹ thuật" });
        }

        return Ok(_mapper.Map<ProductSpecificationDto>(specification));
    }

    /// <summary>
    /// Tạo mới một thông số kỹ thuật
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ProductSpecificationDto>> CreateSpecification(
        Guid productId,
        [FromBody] CreateProductSpecificationRequest request)
    {
        // Kiểm tra sản phẩm tồn tại
        var product = await _context.Products.FindAsync(productId);
        if (product == null)
        {
            return NotFound(new { message = "Không tìm thấy sản phẩm" });
        }

        var specification = _mapper.Map<Domain.Entities.ProductSpecification>(request);
        specification.ProductId = productId;

        _context.ProductSpecifications.Add(specification);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetSpecification),
            new { productId, id = specification.Id },
            _mapper.Map<ProductSpecificationDto>(specification));
    }

    /// <summary>
    /// Cập nhật thông số kỹ thuật
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductSpecificationDto>> UpdateSpecification(
        Guid productId,
        Guid id,
        [FromBody] UpdateProductSpecificationRequest request)
    {
        var specification = await _context.ProductSpecifications
            .FirstOrDefaultAsync(ps => ps.Id == id && ps.ProductId == productId);

        if (specification == null)
        {
            return NotFound(new { message = "Không tìm thấy thông số kỹ thuật" });
        }

        _mapper.Map(request, specification);
        await _context.SaveChangesAsync();

        return Ok(_mapper.Map<ProductSpecificationDto>(specification));
    }

    /// <summary>
    /// Xóa thông số kỹ thuật
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteSpecification(Guid productId, Guid id)
    {
        var specification = await _context.ProductSpecifications
            .FirstOrDefaultAsync(ps => ps.Id == id && ps.ProductId == productId);

        if (specification == null)
        {
            return NotFound(new { message = "Không tìm thấy thông số kỹ thuật" });
        }

        _context.ProductSpecifications.Remove(specification);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Cập nhật hàng loạt thông số kỹ thuật (xóa hết cũ, thêm mới)
    /// </summary>
    [HttpPut("bulk")]
    public async Task<ActionResult<List<ProductSpecificationDto>>> BulkUpdateSpecifications(
        Guid productId,
        [FromBody] BulkUpdateProductSpecificationsRequest request)
    {
        // Kiểm tra sản phẩm tồn tại
        var product = await _context.Products.FindAsync(productId);
        if (product == null)
        {
            return NotFound(new { message = "Không tìm thấy sản phẩm" });
        }

        // Xóa tất cả thông số cũ
        var existingSpecs = await _context.ProductSpecifications
            .Where(ps => ps.ProductId == productId)
            .ToListAsync();
        _context.ProductSpecifications.RemoveRange(existingSpecs);

        // Thêm thông số mới
        var newSpecs = request.Specifications.Select((req, index) =>
        {
            var spec = _mapper.Map<Domain.Entities.ProductSpecification>(req);
            spec.ProductId = productId;
            spec.DisplayOrder = req.DisplayOrder != 0 ? req.DisplayOrder : index;
            return spec;
        }).ToList();

        _context.ProductSpecifications.AddRange(newSpecs);
        await _context.SaveChangesAsync();

        return Ok(_mapper.Map<List<ProductSpecificationDto>>(newSpecs.OrderBy(s => s.DisplayOrder)));
    }
}
