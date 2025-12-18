namespace PickleBall.Domain.Entities;

public class Voucher : BaseEntity
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
    public bool IsPublic { get; set; } = false;

    public int? UsageLimitTotal { get; set; }
    public int? UsageLimitPerCustomer { get; set; }
    public int UsedCount { get; set; }

    public ICollection<VoucherUsage> Usages { get; set; } = new List<VoucherUsage>();
}

public class VoucherUsage : BaseEntity
{
    public Guid VoucherId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? UserId { get; set; }
    public string? GuestEmail { get; set; }

    public Voucher? Voucher { get; set; }
    public Order? Order { get; set; }
    public User? User { get; set; }
}

