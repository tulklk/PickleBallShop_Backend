namespace PickleBall.Domain.Entities;

public class Payment : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public PaymentMethod PaymentGateway { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string? TransactionId { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? RawResponse { get; set; } // JSON string for debugging

    // Navigation properties
    public Order Order { get; set; } = null!;
    public User User { get; set; } = null!;
}
