using FluentValidation;
using PickleBall.Application.DTOs.Products;

namespace PickleBall.Application.Validators;

public class ProductQueryDtoValidator : AbstractValidator<ProductQueryDto>
{
    private static readonly string[] AllowedSorts =
    {
        "", "name", "price", "price_desc", "newest", "popularity"
    };

    public ProductQueryDtoValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100");

        RuleFor(x => x.SortBy)
            .Must(s => string.IsNullOrWhiteSpace(s) || AllowedSorts.Contains(s.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("SortBy must be one of: name, price, price_desc, newest, popularity, or omitted");

        RuleFor(x => x)
            .Must(q => !q.MinPrice.HasValue || !q.MaxPrice.HasValue || q.MinPrice <= q.MaxPrice)
            .WithMessage("MinPrice cannot be greater than MaxPrice");
    }
}
