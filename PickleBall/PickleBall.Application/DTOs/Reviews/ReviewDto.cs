namespace PickleBall.Application.DTOs.Reviews;

public class ReviewDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid? UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    // NOTE: reviewer PhoneNumber / Email are collected for the shop but are never returned by the public API (PII).
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? ReviewImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ReviewsResponseDto
{
    public List<ReviewDto> Reviews { get; set; } = new();
    public int TotalCount { get; set; }
    public double? AverageRating { get; set; }
    public Dictionary<int, int> RatingDistribution { get; set; } = new(); // Key: Rating (1-5), Value: Count
}

