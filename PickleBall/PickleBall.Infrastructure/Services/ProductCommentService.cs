using AutoMapper;
using Microsoft.EntityFrameworkCore;
using PickleBall.Application.DTOs.Comments;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;

namespace PickleBall.Infrastructure.Services;

public class ProductCommentService : IProductCommentService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public ProductCommentService(ApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<CommentsResponseDto> GetProductCommentsAsync(Guid productId)
    {
        // Verify product exists
        var productExists = await _context.Products.AnyAsync(p => p.Id == productId && p.IsActive);
        if (!productExists)
        {
            throw new KeyNotFoundException("Product not found");
        }

        // Get all parent comments (where ParentCommentId is null) with user info
        var parentComments = await _context.ProductComments
            .Include(c => c.User)
            .Include(c => c.Replies)
                .ThenInclude(r => r.User)
            .Where(c => c.ProductId == productId && c.ParentCommentId == null)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        // Map to DTOs
        var commentDtos = parentComments.Select(c => MapToDto(c)).ToList();

        return new CommentsResponseDto
        {
            Comments = commentDtos,
            TotalCount = commentDtos.Count
        };
    }

    public async Task<CommentDto> CreateCommentAsync(Guid userId, Guid productId, CreateCommentRequest request)
    {
        // Verify product exists
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);
        if (product == null)
        {
            throw new KeyNotFoundException("Product not found");
        }

        // Verify user exists
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        // Validate content
        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > 2000)
        {
            throw new ArgumentException("Comment content is required and must be less than 2000 characters");
        }

        // Create comment
        var comment = new ProductComment
        {
            ProductId = productId,
            UserId = userId,
            ParentCommentId = null, // Parent comment
            Content = request.Content.Trim(),
            IsEdited = false
        };

        _context.ProductComments.Add(comment);
        await _context.SaveChangesAsync();

        // Reload with user info
        var createdComment = await _context.ProductComments
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == comment.Id);

        return MapToDto(createdComment!);
    }

    public async Task<CommentDto> ReplyToCommentAsync(Guid userId, Guid productId, Guid parentCommentId, ReplyCommentRequest request)
    {
        // Verify product exists
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);
        if (product == null)
        {
            throw new KeyNotFoundException("Product not found");
        }

        // Verify parent comment exists and belongs to the product
        var parentComment = await _context.ProductComments
            .FirstOrDefaultAsync(c => c.Id == parentCommentId && c.ProductId == productId);
        if (parentComment == null)
        {
            throw new KeyNotFoundException("Parent comment not found");
        }

        // Verify user exists
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        // Validate content
        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > 2000)
        {
            throw new ArgumentException("Reply content is required and must be less than 2000 characters");
        }

        // Create reply
        var reply = new ProductComment
        {
            ProductId = productId,
            UserId = userId,
            ParentCommentId = parentCommentId,
            Content = request.Content.Trim(),
            IsEdited = false
        };

        _context.ProductComments.Add(reply);
        await _context.SaveChangesAsync();

        // Reload with user info
        var createdReply = await _context.ProductComments
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == reply.Id);

        return MapToDto(createdReply!);
    }

    public async Task<bool> UpdateCommentAsync(Guid userId, Guid commentId, CreateCommentRequest request)
    {
        var comment = await _context.ProductComments.FindAsync(commentId);
        if (comment == null)
        {
            throw new KeyNotFoundException("Comment not found");
        }

        // Verify user owns the comment
        if (comment.UserId != userId)
        {
            throw new UnauthorizedAccessException("You can only update your own comments");
        }

        // Validate content
        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > 2000)
        {
            throw new ArgumentException("Comment content is required and must be less than 2000 characters");
        }

        comment.Content = request.Content.Trim();
        comment.IsEdited = true;
        comment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteCommentAsync(Guid userId, Guid commentId, bool isAdmin)
    {
        var comment = await _context.ProductComments
            .Include(c => c.Replies)
            .FirstOrDefaultAsync(c => c.Id == commentId);

        if (comment == null)
        {
            throw new KeyNotFoundException("Comment not found");
        }

        // Verify user owns the comment or is admin
        if (comment.UserId != userId && !isAdmin)
        {
            throw new UnauthorizedAccessException("You can only delete your own comments");
        }

        // If deleting parent comment, also delete all replies (cascade delete)
        _context.ProductComments.Remove(comment);
        await _context.SaveChangesAsync();

        return true;
    }

    private CommentDto MapToDto(ProductComment comment)
    {
        var dto = new CommentDto
        {
            Id = comment.Id,
            ProductId = comment.ProductId,
            UserId = comment.UserId,
            UserName = comment.User.FullName,
            UserAvatar = comment.User.Avatar,
            IsAdmin = comment.User.Role == Role.Admin,
            ParentCommentId = comment.ParentCommentId,
            Content = comment.Content,
            IsEdited = comment.IsEdited,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt,
            Replies = comment.Replies?.Select(r => MapToDto(r)).OrderBy(r => r.CreatedAt).ToList() ?? new List<CommentDto>()
        };

        return dto;
    }
}

