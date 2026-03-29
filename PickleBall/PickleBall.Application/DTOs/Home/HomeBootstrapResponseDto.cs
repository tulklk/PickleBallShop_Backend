using PickleBall.Application.DTOs.Categories;
using PickleBall.Application.DTOs.News;
using PickleBall.Application.DTOs.Products;

namespace PickleBall.Application.DTOs.Home;

public class HomeBootstrapResponseDto
{
    public List<ProductListDto> FeaturedProducts { get; set; } = new();
    public List<CategoryDto> CategoryTree { get; set; } = new();
    public List<NewsListDto> LatestNews { get; set; } = new();
}
