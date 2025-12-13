namespace PickleBall.Domain.Entities;

public class ProductReview : BaseEntity
{
    public Guid ProductId { get; set; }
    public Guid? UserId { get; set; } // Nullable để hỗ trợ feedback không cần đăng nhập
    
    // Thông tin người đánh giá (cho trường hợp không đăng nhập)
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    
    public int Rating { get; set; } // 1-5
    public string? Comment { get; set; }
    public string? ReviewImageUrl { get; set; } // URL ảnh đánh giá (nếu có)

    // Navigation properties
    public Product Product { get; set; } = null!;
    public User? User { get; set; } // Nullable vì có thể không có user
}
