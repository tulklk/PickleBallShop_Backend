namespace PickleBall.Domain.Entities;

public class InventoryTransaction : BaseEntity
{
    public Guid ProductId { get; set; }
    public int ChangeQuantity { get; set; }
    public string Reason { get; set; } = string.Empty; // "Order", "Restock", "ManualAdjust", etc.

    // Navigation property
    public Product Product { get; set; } = null!;
}
