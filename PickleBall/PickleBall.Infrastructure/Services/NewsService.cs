using AutoMapper;
using Microsoft.EntityFrameworkCore;
using PickleBall.Application.DTOs.News;
using PickleBall.Application.DTOs.Products;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;
using PickleBall.Infrastructure.Helpers;

namespace PickleBall.Infrastructure.Services;

public class NewsService : INewsService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public NewsService(ApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<PagedResultDto<NewsListDto>> GetNewsAsync(NewsQueryDto query)
    {
        var newsQuery = _context.NewsPosts
            .Include(n => n.Author)
            .AsQueryable();

        // Filter by published status
        if (query.IsPublished.HasValue)
        {
            newsQuery = newsQuery.Where(n => n.IsPublished == query.IsPublished.Value);
        }

        // Filter by category
        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            newsQuery = newsQuery.Where(n => n.Category == query.Category);
        }

        // Search by title or content
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm.ToLower();
            newsQuery = newsQuery.Where(n =>
                n.Title.ToLower().Contains(searchTerm) ||
                (n.Summary != null && n.Summary.ToLower().Contains(searchTerm)) ||
                n.Content.ToLower().Contains(searchTerm));
        }

        // Apply sorting
        newsQuery = query.SortBy?.ToLower() switch
        {
            "popular" => newsQuery.OrderByDescending(n => n.ViewCount),
            "oldest" => newsQuery.OrderBy(n => n.PublishedAt ?? n.CreatedAt),
            _ => newsQuery.OrderByDescending(n => n.PublishedAt ?? n.CreatedAt) // newest by default
        };

        var totalCount = await newsQuery.CountAsync();

        var items = await newsQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResultDto<NewsListDto>
        {
            Items = _mapper.Map<List<NewsListDto>>(items),
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<NewsDto?> GetNewsByIdAsync(Guid id)
    {
        var news = await _context.NewsPosts
            .Include(n => n.Author)
            .FirstOrDefaultAsync(n => n.Id == id);

        return news != null ? _mapper.Map<NewsDto>(news) : null;
    }

    public async Task<NewsDto?> GetNewsBySlugAsync(string slug)
    {
        var news = await _context.NewsPosts
            .Include(n => n.Author)
            .FirstOrDefaultAsync(n => n.Slug == slug);

        return news != null ? _mapper.Map<NewsDto>(news) : null;
    }

    public async Task<NewsDto> CreateNewsAsync(Guid authorId, CreateNewsRequest request)
    {
        // Verify author exists
        var author = await _context.Users.FindAsync(authorId);
        if (author == null)
        {
            throw new KeyNotFoundException("Author not found");
        }

        // Generate slug from title
        var baseSlug = SlugHelper.GenerateSlug(request.Title);
        var slug = baseSlug;
        var counter = 1;

        // Ensure slug is unique
        while (await _context.NewsPosts.AnyAsync(n => n.Slug == slug))
        {
            slug = $"{baseSlug}-{counter}";
            counter++;
        }

        var news = new NewsPost
        {
            Title = request.Title.Trim(),
            Slug = slug,
            Content = request.Content.Trim(),
            Summary = !string.IsNullOrWhiteSpace(request.Summary) ? request.Summary.Trim() : null,
            ThumbnailUrl = request.ThumbnailUrl.Trim(),
            AuthorId = authorId,
            Category = request.Category.Trim(),
            Tags = !string.IsNullOrWhiteSpace(request.Tags) ? request.Tags.Trim() : null,
            IsPublished = request.IsPublished,
            PublishedAt = request.IsPublished ? DateTime.UtcNow : null
        };

        _context.NewsPosts.Add(news);
        await _context.SaveChangesAsync();

        // Reload with author info
        var createdNews = await _context.NewsPosts
            .Include(n => n.Author)
            .FirstOrDefaultAsync(n => n.Id == news.Id);

        return _mapper.Map<NewsDto>(createdNews!);
    }

    public async Task<NewsDto?> UpdateNewsAsync(Guid id, UpdateNewsRequest request)
    {
        var news = await _context.NewsPosts.FindAsync(id);
        if (news == null)
        {
            return null;
        }

        // Update slug if title changed
        if (news.Title != request.Title)
        {
            var baseSlug = SlugHelper.GenerateSlug(request.Title);
            var slug = baseSlug;
            var counter = 1;

            while (await _context.NewsPosts.AnyAsync(n => n.Slug == slug && n.Id != id))
            {
                slug = $"{baseSlug}-{counter}";
                counter++;
            }

            news.Slug = slug;
        }

        news.Title = request.Title.Trim();
        news.Content = request.Content.Trim();
        news.Summary = !string.IsNullOrWhiteSpace(request.Summary) ? request.Summary.Trim() : null;
        news.ThumbnailUrl = request.ThumbnailUrl.Trim();
        news.Category = request.Category.Trim();
        news.Tags = !string.IsNullOrWhiteSpace(request.Tags) ? request.Tags.Trim() : null;
        
        // Set PublishedAt when publishing for the first time
        if (request.IsPublished && !news.IsPublished)
        {
            news.PublishedAt = DateTime.UtcNow;
        }
        
        news.IsPublished = request.IsPublished;
        news.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Reload with author info
        var updatedNews = await _context.NewsPosts
            .Include(n => n.Author)
            .FirstOrDefaultAsync(n => n.Id == id);

        return _mapper.Map<NewsDto>(updatedNews!);
    }

    public async Task<bool> DeleteNewsAsync(Guid id)
    {
        var news = await _context.NewsPosts.FindAsync(id);
        if (news == null)
        {
            return false;
        }

        _context.NewsPosts.Remove(news);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IncrementViewCountAsync(Guid id)
    {
        var news = await _context.NewsPosts.FindAsync(id);
        if (news == null)
        {
            return false;
        }

        news.ViewCount++;
        await _context.SaveChangesAsync();
        return true;
    }
}

