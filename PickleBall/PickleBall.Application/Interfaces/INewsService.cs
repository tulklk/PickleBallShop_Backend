using PickleBall.Application.DTOs.News;
using PickleBall.Application.DTOs.Products;

namespace PickleBall.Application.Interfaces;

public interface INewsService
{
    Task<PagedResultDto<NewsListDto>> GetNewsAsync(NewsQueryDto query);
    Task<NewsDto?> GetNewsByIdAsync(Guid id);
    Task<NewsDto?> GetNewsBySlugAsync(string slug);
    Task<NewsDto> CreateNewsAsync(Guid authorId, CreateNewsRequest request);
    Task<NewsDto?> UpdateNewsAsync(Guid id, UpdateNewsRequest request);
    Task<bool> DeleteNewsAsync(Guid id);
    Task<bool> IncrementViewCountAsync(Guid id);
}

