namespace PickleBall.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Avatar { get; set; }
    public string? GoogleId { get; set; }
    public Role Role { get; set; } = Role.Customer;
    public bool IsActive { get; set; } = true;
    public bool EmailVerified { get; set; } = false;

    /// <summary>SHA-256 (hex) of the single-use password reset token. The raw token is only ever emailed.</summary>
    public string? PasswordResetTokenHash { get; set; }
    public DateTime? PasswordResetExpiresAt { get; set; }

    // Navigation properties
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<Cart> Carts { get; set; } = new List<Cart>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();
    public ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
}
