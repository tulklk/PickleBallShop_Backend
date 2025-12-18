using PickleBall.Application.DTOs.Products;
using PickleBall.Application.DTOs.Vouchers;

namespace PickleBall.Application.Interfaces;

public interface IVoucherService
{
    // Admin
    Task<PagedResultDto<VoucherDto>> GetVouchersAsync(int page, int pageSize, bool? isPublic = null);
    Task<VoucherDto?> GetByIdAsync(Guid id);
    Task<VoucherDto> CreateAsync(CreateVoucherRequest request);
    Task<VoucherDto> UpdateAsync(Guid id, UpdateVoucherRequest request);
    Task DeleteAsync(Guid id);
    Task<VoucherDto?> TogglePublishAsync(Guid id, bool isPublic);

    // Checkout
    Task<ApplyVoucherResponse> ApplyForUserAsync(Guid userId, decimal orderTotal, string code);
    Task<ApplyVoucherResponse> ApplyForGuestAsync(string email, decimal orderTotal, string code);

    // Used internally by order creation to validate and compute discount again
    Task<(bool isValid, string message, decimal discountAmount, VoucherDto? voucher)> ValidateAndCalculateAsync(
        string code,
        decimal orderTotal,
        Guid? userId,
        string? guestEmail);
}

