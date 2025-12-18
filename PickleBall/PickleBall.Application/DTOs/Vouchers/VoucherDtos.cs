using PickleBall.Domain.Entities;

namespace PickleBall.Application.DTOs.Vouchers;

public class VoucherDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public VoucherDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsPublic { get; set; }
    public int? UsageLimitTotal { get; set; }
    public int? UsageLimitPerCustomer { get; set; }
    public int UsedCount { get; set; }
}

public class CreateVoucherRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public VoucherDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsPublic { get; set; }
    public int? UsageLimitTotal { get; set; }
    public int? UsageLimitPerCustomer { get; set; }
}

public class UpdateVoucherRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public VoucherDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsPublic { get; set; }
    public int? UsageLimitTotal { get; set; }
    public int? UsageLimitPerCustomer { get; set; }
}

public class ApplyVoucherRequest
{
    public string Code { get; set; } = string.Empty;
    public decimal OrderTotal { get; set; }
}

public class ApplyVoucherResponse
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public decimal DiscountAmount { get; set; }
    public decimal FinalTotal { get; set; }
    public VoucherDto? Voucher { get; set; }
}

