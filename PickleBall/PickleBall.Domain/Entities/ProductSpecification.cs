namespace PickleBall.Domain.Entities;

public class ProductSpecification : BaseEntity
{
    public Guid ProductId { get; set; }
    public string Key { get; set; } = string.Empty; // Ví dụ: "Trọng lượng", "Độ dày mặt vợt"
    public string Value { get; set; } = string.Empty; // Ví dụ: "Trung bình: 7.8 - 8.2 oz"
    public int DisplayOrder { get; set; } = 0; // Thứ tự hiển thị

    // Navigation property
    public Product? Product { get; set; }
}
