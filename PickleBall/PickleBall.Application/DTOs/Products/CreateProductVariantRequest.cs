namespace PickleBall.Application.DTOs.Products;

public class CreateProductVariantRequest
{
    public string? Color { get; set; } // Màu sắc (cho vợt)
    public string? Size { get; set; } // Size (cho giày)
    public string? Spec { get; set; } // Thông số (cho vợt: 13mm, 15mm, etc.)
    public decimal Price { get; set; } // Giá của variant (giá tuyệt đối)
    public decimal? OriginalPrice { get; set; } // Giá gốc của variant (nếu có giảm giá)
    public int StockQuantity { get; set; } = 0; // Số lượng tồn kho
    public string SKU { get; set; } = string.Empty; // SKU riêng cho variant (phải unique)
    public bool IsActive { get; set; } = true;
}

