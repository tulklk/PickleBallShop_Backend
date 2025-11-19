using PickleBall.Application.DTOs.Products;

namespace PickleBall.Application.Interfaces;

public interface IProductService
{
    Task<PagedResultDto<ProductListDto>> GetProductsAsync(ProductQueryDto query);
    Task<ProductDto?> GetProductByIdAsync(Guid id);
    Task<ProductDto?> GetProductBySlugAsync(string slug);
    Task<List<ProductListDto>> GetFeaturedProductsAsync(int count = 10);
    Task<List<ProductListDto>> SearchProductsAsync(string searchTerm, int page = 1, int pageSize = 20);
}
