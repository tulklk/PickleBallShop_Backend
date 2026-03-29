using FluentValidation;
using PickleBall.Application.DTOs.News;

namespace PickleBall.Application.Validators;

public class NewsQueryDtoValidator : AbstractValidator<NewsQueryDto>
{
    private static readonly string[] AllowedSorts =
    {
        "", "newest", "popular", "oldest"
    };

    public NewsQueryDtoValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100");

        RuleFor(x => x.SortBy)
            .Must(s => string.IsNullOrWhiteSpace(s) || AllowedSorts.Contains(s.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("SortBy must be one of: newest, popular, oldest, or omitted");
    }
}
