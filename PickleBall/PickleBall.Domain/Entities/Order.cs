namespace PickleBall.Domain.Entities;

public class Order : BaseEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public Guid? UserId { get; set; } // Nullable for guest checkout
    public decimal TotalAmount { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public OrderStatus OrderStatus { get; set; } = OrderStatus.Processing;
    public PaymentMethod? PaymentMethod { get; set; }
    public string? PaymentTransactionId { get; set; }

    // Guest checkout fields (used when UserId is null)
    public string? GuestEmail { get; set; }
    public string? GuestFullName { get; set; }

    // Shipping address fields
    public string ShippingFullName { get; set; } = string.Empty;
    public string ShippingPhoneNumber { get; set; } = string.Empty;
    public string ShippingAddressLine { get; set; } = string.Empty;
    public string? ShippingWard { get; set; }
    public string? ShippingDistrict { get; set; }
    public string? ShippingCity { get; set; }

    // Voucher
    public Guid? VoucherId { get; set; }
    public decimal? VoucherDiscountAmount { get; set; }

    // Navigation properties
    public User? User { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public Voucher? Voucher { get; set; }
}
