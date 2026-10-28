using PickleBall.Application.Mapping;
using Microsoft.EntityFrameworkCore;
using PickleBall.Application.DTOs.Reviews;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;

namespace PickleBall.Infrastructure.Services;

public class ProductReviewService : IProductReviewService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public ProductReviewService(ApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<ReviewsResponseDto> GetProductReviewsAsync(Guid productId)
    {
        // Verify product exists
        var productExists = await _context.Products.AnyAsync(p => p.Id == productId && p.IsActive);
        if (!productExists)
        {
            throw new KeyNotFoundException("Product not found");
        }

        // Get all reviews for this product
        var reviews = await _context.ProductReviews
            .Where(r => r.ProductId == productId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        // Calculate average rating
        double? averageRating = reviews.Any() ? reviews.Average(r => r.Rating) : null;

        // Calculate rating distribution
        var ratingDistribution = new Dictionary<int, int>();
        for (int i = 1; i <= 5; i++)
        {
            ratingDistribution[i] = reviews.Count(r => r.Rating == i);
        }

        var reviewDtos = _mapper.Map<List<ReviewDto>>(reviews);

        return new ReviewsResponseDto
        {
            Reviews = reviewDtos,
            TotalCount = reviewDtos.Count,
            AverageRating = averageRating,
            RatingDistribution = ratingDistribution
        };
    }

    public async Task<ReviewsResponseDto> GetProductReviewsBySlugAsync(string slug)
    {
        // Find product by slug
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsActive);

        if (product == null)
        {
            throw new KeyNotFoundException("Product not found");
        }

        // Use existing method with productId
        return await GetProductReviewsAsync(product.Id);
    }

    public async Task<ReviewDto> CreateReviewAsync(Guid? userId, Guid productId, CreateReviewRequest request)
    {
        // Verify product exists
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);
        if (product == null)
        {
            throw new KeyNotFoundException("Product not found");
        }

        // If userId is provided, verify user exists
        if (userId.HasValue)
        {
            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
            {
                throw new KeyNotFoundException("User not found");
            }

            // Check if user already reviewed this product
            var existingReview = await _context.ProductReviews
                .FirstOrDefaultAsync(r => r.ProductId == productId && r.UserId == userId.Value);
            
            if (existingReview != null)
            {
                throw new ArgumentException("Bạn đã đánh giá sản phẩm này rồi");
            }
        }

        // Validate rating
        if (request.Rating < 1 || request.Rating > 5)
        {
            throw new ArgumentException("Rating must be between 1 and 5");
        }

        // Validate comment if provided
        if (!string.IsNullOrWhiteSpace(request.Comment) && request.Comment.Length < 10)
        {
            throw new ArgumentException("Bình luận phải có ít nhất 10 ký tự");
        }

        // Create review
        var review = new ProductReview
        {
            ProductId = productId,
            UserId = userId,
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            Email = !string.IsNullOrWhiteSpace(request.Email) ? request.Email.Trim() : null,
            Rating = request.Rating,
            Comment = !string.IsNullOrWhiteSpace(request.Comment) ? request.Comment.Trim() : null,
            ReviewImageUrl = !string.IsNullOrWhiteSpace(request.ReviewImageUrl) ? request.ReviewImageUrl.Trim() : null
        };

        _context.ProductReviews.Add(review);
        await _context.SaveChangesAsync();

        // Reload to get full entity
        var createdReview = await _context.ProductReviews
            .FirstOrDefaultAsync(r => r.Id == review.Id);

        return _mapper.Map<ReviewDto>(createdReview!);
    }

    public async Task<ReviewDto> CreateReviewBySlugAsync(Guid? userId, string slug, CreateReviewRequest request)
    {
        // Find product by slug
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Slug == slug && p.IsActive);
        if (product == null)
        {
            throw new KeyNotFoundException("Product not found");
        }

        // Use existing method with productId
        return await CreateReviewAsync(userId, product.Id, request);
    }
}

