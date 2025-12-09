namespace PickleBall.Application.DTOs.Comments;

public class CommentDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? UserAvatar { get; set; }
    public bool IsAdmin { get; set; } // Admin hay user thường
    public Guid? ParentCommentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsEdited { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<CommentDto> Replies { get; set; } = new(); // Danh sách replies
}

public class CommentsResponseDto
{
    public List<CommentDto> Comments { get; set; } = new();
    public int TotalCount { get; set; }
}

