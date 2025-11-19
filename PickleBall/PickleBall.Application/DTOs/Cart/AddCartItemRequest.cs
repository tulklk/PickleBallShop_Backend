namespace PickleBall.Application.DTOs.Cart;

public class AddCartItemRequest
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; } = 1;
}
