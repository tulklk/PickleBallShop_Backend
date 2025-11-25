namespace PickleBall.Application.DTOs.Payments;

public class CreatePaymentResponse
{
    public string? PaymentUrl { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public bool IsCod { get; set; } = false;
}
