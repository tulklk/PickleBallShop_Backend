using PickleBall.Application.DTOs.Reviews;

namespace PickleBall.Application.Interfaces;

public interface IProductReviewService
{
    Task<ReviewsResponseDto> GetProductReviewsAsync(Guid productId);
    Task<ReviewsResponseDto> GetProductReviewsBySlugAsync(string slug);
    Task<ReviewDto> CreateReviewAsync(Guid? userId, Guid productId, CreateReviewRequest request);
    Task<ReviewDto> CreateReviewBySlugAsync(Guid? userId, string slug, CreateReviewRequest request);
}

