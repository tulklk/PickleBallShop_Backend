namespace PickleBall.Application.DTOs.Orders;

public class ApplyVoucherForCartRequest
{
    public string Code { get; set; } = string.Empty;
}

public class ApplyVoucherForGuestOrderRequest
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public List<GuestCartItemDto> Items { get; set; } = new();
}

