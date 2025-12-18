using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PickleBall.Application.DTOs.Vouchers;
using PickleBall.Application.Interfaces;

namespace PickleBall.Controllers.Admin;

[ApiController]
[Route("api/admin/vouchers")]
[Authorize(Roles = "Admin")]
public class AdminVouchersController : ControllerBase
{
    private readonly IVoucherService _voucherService;

    public AdminVouchersController(IVoucherService voucherService)
    {
        _voucherService = voucherService;
    }

    [HttpGet]
    public async Task<ActionResult> GetVouchers([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool? isPublic = null)
    {
        var result = await _voucherService.GetVouchersAsync(page, pageSize, isPublic);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<VoucherDto>> GetById(Guid id)
    {
        var voucher = await _voucherService.GetByIdAsync(id);
        if (voucher == null)
        {
            return NotFound(new { message = "Không tìm thấy mã giảm giá" });
        }

        return Ok(voucher);
    }

    [HttpPost]
    public async Task<ActionResult<VoucherDto>> Create([FromBody] CreateVoucherRequest request)
    {
        try
        {
            var voucher = await _voucherService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = voucher.Id }, voucher);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<VoucherDto>> Update(Guid id, [FromBody] UpdateVoucherRequest request)
    {
        try
        {
            var voucher = await _voucherService.UpdateAsync(id, request);
            return Ok(voucher);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Không tìm thấy mã giảm giá" });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        await _voucherService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPatch("{id:guid}/publish")]
    public async Task<ActionResult<VoucherDto>> TogglePublish(Guid id, [FromQuery] bool isPublic)
    {
        var voucher = await _voucherService.TogglePublishAsync(id, isPublic);
        if (voucher == null)
        {
            return NotFound(new { message = "Không tìm thấy mã giảm giá" });
        }

        return Ok(voucher);
    }
}

