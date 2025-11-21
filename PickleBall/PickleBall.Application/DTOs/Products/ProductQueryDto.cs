namespace PickleBall.Application.DTOs.Products;

public class ProductQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public Guid? CategoryId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? SortBy { get; set; } // price, newest, popularity
    public string? Brand { get; set; }
    public string? SearchTerm { get; set; }
}
