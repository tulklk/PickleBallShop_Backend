using PickleBall.Domain.Entities;

namespace PickleBall.Application.DTOs.Orders;

public class CreateGuestOrderRequest
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public ShippingAddressDto ShippingAddress { get; set; } = null!;
    public PaymentMethod PaymentMethod { get; set; }
    public List<GuestCartItemDto> Items { get; set; } = new();
}

public class GuestCartItemDto
{
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public int Quantity { get; set; } = 1;
}

