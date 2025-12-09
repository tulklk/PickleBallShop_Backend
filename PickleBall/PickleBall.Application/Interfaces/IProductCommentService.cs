using PickleBall.Application.DTOs.Comments;

namespace PickleBall.Application.Interfaces;

public interface IProductCommentService
{
    Task<CommentsResponseDto> GetProductCommentsAsync(Guid productId);
    Task<CommentDto> CreateCommentAsync(Guid userId, Guid productId, CreateCommentRequest request);
    Task<CommentDto> ReplyToCommentAsync(Guid userId, Guid productId, Guid parentCommentId, ReplyCommentRequest request);
    Task<bool> UpdateCommentAsync(Guid userId, Guid commentId, CreateCommentRequest request);
    Task<bool> DeleteCommentAsync(Guid userId, Guid commentId, bool isAdmin);
}

