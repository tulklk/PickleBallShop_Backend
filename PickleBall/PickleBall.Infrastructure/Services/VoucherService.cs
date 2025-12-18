using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PickleBall.Application.DTOs.Products;
using PickleBall.Application.DTOs.Vouchers;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;

namespace PickleBall.Infrastructure.Services;

public class VoucherService : IVoucherService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<VoucherService> _logger;

    public VoucherService(ApplicationDbContext context, IMapper mapper, ILogger<VoucherService> logger)
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<PagedResultDto<VoucherDto>> GetVouchersAsync(int page, int pageSize, bool? isPublic = null)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0 || pageSize > 100) pageSize = 20;

        var query = _context.Vouchers.AsNoTracking();

        if (isPublic.HasValue)
        {
            query = query.Where(v => v.IsPublic == isPublic.Value);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(v => v.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ProjectTo<VoucherDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        return new PagedResultDto<VoucherDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<VoucherDto?> GetByIdAsync(Guid id)
    {
        var voucher = await _context.Vouchers
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id);

        return voucher == null ? null : _mapper.Map<VoucherDto>(voucher);
    }

    public async Task<VoucherDto> CreateAsync(CreateVoucherRequest request)
    {
        var normalizedCode = request.Code.Trim().ToUpperInvariant();

        var exists = await _context.Vouchers.AnyAsync(v => v.Code == normalizedCode);
        if (exists)
        {
            throw new InvalidOperationException("Mã giảm giá đã tồn tại");
        }

        var voucher = _mapper.Map<Voucher>(request);
        voucher.Code = normalizedCode;

        _context.Vouchers.Add(voucher);
        await _context.SaveChangesAsync();

        return _mapper.Map<VoucherDto>(voucher);
    }

    public async Task<VoucherDto> UpdateAsync(Guid id, UpdateVoucherRequest request)
    {
        var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Id == id);
        if (voucher == null)
        {
            throw new KeyNotFoundException("Không tìm thấy mã giảm giá");
        }

        _mapper.Map(request, voucher);

        await _context.SaveChangesAsync();

        return _mapper.Map<VoucherDto>(voucher);
    }

    public async Task DeleteAsync(Guid id)
    {
        var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Id == id);
        if (voucher == null)
        {
            return;
        }

        _context.Vouchers.Remove(voucher);
        await _context.SaveChangesAsync();
    }

    public async Task<VoucherDto?> TogglePublishAsync(Guid id, bool isPublic)
    {
        var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Id == id);
        if (voucher == null)
        {
            return null;
        }

        voucher.IsPublic = isPublic;
        await _context.SaveChangesAsync();

        return _mapper.Map<VoucherDto>(voucher);
    }

    public async Task<ApplyVoucherResponse> ApplyForUserAsync(Guid userId, decimal orderTotal, string code)
    {
        var (isValid, message, discountAmount, voucher) =
            await ValidateAndCalculateAsync(code, orderTotal, userId, null);

        return BuildApplyResponse(orderTotal, isValid, message, discountAmount, voucher);
    }

    public async Task<ApplyVoucherResponse> ApplyForGuestAsync(string email, decimal orderTotal, string code)
    {
        var (isValid, message, discountAmount, voucher) =
            await ValidateAndCalculateAsync(code, orderTotal, null, email);

        return BuildApplyResponse(orderTotal, isValid, message, discountAmount, voucher);
    }

    public async Task<(bool isValid, string message, decimal discountAmount, VoucherDto? voucher)> ValidateAndCalculateAsync(
        string code,
        decimal orderTotal,
        Guid? userId,
        string? guestEmail)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return (false, "Mã giảm giá không được để trống", 0, null);
        }

        var normalizedCode = code.Trim().ToUpperInvariant();
        var now = DateTime.UtcNow;

        var voucher = await _context.Vouchers
            .FirstOrDefaultAsync(v => v.Code == normalizedCode);

        if (voucher == null)
        {
            return (false, "Mã giảm giá không hợp lệ", 0, null);
        }

        if (!voucher.IsPublic)
        {
            return (false, "Mã giảm giá hiện không khả dụng", 0, null);
        }

        if (now < voucher.StartDate || now > voucher.EndDate)
        {
            return (false, "Mã giảm giá đã hết hạn hoặc chưa bắt đầu", 0, _mapper.Map<VoucherDto>(voucher));
        }

        if (voucher.MinOrderAmount.HasValue && orderTotal < voucher.MinOrderAmount.Value)
        {
            return (false, $"Đơn hàng phải từ {voucher.MinOrderAmount.Value:N0}đ trở lên để sử dụng mã này", 0, _mapper.Map<VoucherDto>(voucher));
        }

        if (voucher.UsageLimitTotal.HasValue && voucher.UsedCount >= voucher.UsageLimitTotal.Value)
        {
            return (false, "Mã giảm giá đã được sử dụng tối đa số lần cho phép", 0, _mapper.Map<VoucherDto>(voucher));
        }

        // Check per-customer usage
        if (voucher.UsageLimitPerCustomer.HasValue)
        {
            IQueryable<VoucherUsage> usageQuery = _context.VoucherUsages.Where(u => u.VoucherId == voucher.Id);

            if (userId.HasValue)
            {
                usageQuery = usageQuery.Where(u => u.UserId == userId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(guestEmail))
            {
                usageQuery = usageQuery.Where(u => u.GuestEmail == guestEmail);
            }

            var usedByCustomer = await usageQuery.CountAsync();
            if (usedByCustomer >= voucher.UsageLimitPerCustomer.Value)
            {
                return (false, "Bạn đã sử dụng hết số lần cho phép của mã này", 0, _mapper.Map<VoucherDto>(voucher));
            }
        }

        var discount = CalculateDiscount(orderTotal, voucher);
        if (discount <= 0)
        {
            return (false, "Mã giảm giá không áp dụng được cho đơn hàng này", 0, _mapper.Map<VoucherDto>(voucher));
        }

        return (true, "Áp dụng mã giảm giá thành công", discount, _mapper.Map<VoucherDto>(voucher));
    }

    private static decimal CalculateDiscount(decimal orderTotal, Voucher voucher)
    {
        if (orderTotal <= 0)
        {
            return 0;
        }

        decimal discount = 0;

        if (voucher.DiscountType == VoucherDiscountType.Percent)
        {
            discount = orderTotal * (voucher.DiscountValue / 100m);
            if (voucher.MaxDiscountAmount.HasValue && discount > voucher.MaxDiscountAmount.Value)
            {
                discount = voucher.MaxDiscountAmount.Value;
            }
        }
        else
        {
            discount = voucher.DiscountValue;
        }

        if (discount > orderTotal)
        {
            discount = orderTotal;
        }

        return discount;
    }

    private static ApplyVoucherResponse BuildApplyResponse(
        decimal orderTotal,
        bool isValid,
        string message,
        decimal discountAmount,
        VoucherDto? voucher)
    {
        var finalTotal = orderTotal;
        if (isValid)
        {
            finalTotal = Math.Max(0, orderTotal - discountAmount);
        }

        return new ApplyVoucherResponse
        {
            IsValid = isValid,
            Message = message,
            DiscountAmount = isValid ? discountAmount : 0,
            FinalTotal = finalTotal,
            Voucher = voucher
        };
    }
}

