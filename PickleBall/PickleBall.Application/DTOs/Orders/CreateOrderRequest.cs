using PickleBall.Domain.Entities;

namespace PickleBall.Application.DTOs.Orders;

public class CreateOrderRequest
{
    public ShippingAddressDto ShippingAddress { get; set; } = null!;
    public PaymentMethod PaymentMethod { get; set; }
}

public class ShippingAddressDto
{
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string? Ward { get; set; }
    public string? District { get; set; }
    public string? City { get; set; }
}
