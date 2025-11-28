namespace PickleBall.Domain.Entities;

public class EmailVerificationToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; } = false;

    // Navigation property
    public User User { get; set; } = null!;
}

