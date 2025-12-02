namespace PickleBall.Application.DTOs.Cart;

public class AddCartItemRequest
{
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; } // Variant đã chọn (nếu product có variants)
    public int Quantity { get; set; } = 1;
}
