namespace PickleBall.Application.DTOs.Payments;

public class CreatePaymentResponse
{
    public string PaymentUrl { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
}
