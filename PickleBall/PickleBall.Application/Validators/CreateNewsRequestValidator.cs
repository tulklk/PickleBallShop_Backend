using FluentValidation;
using PickleBall.Application.DTOs.News;

namespace PickleBall.Application.Validators;

public class CreateNewsRequestValidator : AbstractValidator<CreateNewsRequest>
{
    public CreateNewsRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề là bắt buộc")
            .MaximumLength(500).WithMessage("Tiêu đề không được vượt quá 500 ký tự");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Nội dung là bắt buộc")
            .MinimumLength(100).WithMessage("Nội dung phải có ít nhất 100 ký tự")
            .MaximumLength(50000).WithMessage("Nội dung không được vượt quá 50000 ký tự");

        RuleFor(x => x.Summary)
            .MaximumLength(1000).WithMessage("Tóm tắt không được vượt quá 1000 ký tự")
            .When(x => !string.IsNullOrWhiteSpace(x.Summary));

        RuleFor(x => x.ThumbnailUrl)
            .NotEmpty().WithMessage("Ảnh đại diện là bắt buộc")
            .Must(BeValidUrl).WithMessage("URL ảnh không hợp lệ")
            .MaximumLength(1000).WithMessage("URL ảnh không được vượt quá 1000 ký tự");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Danh mục là bắt buộc")
            .MaximumLength(100).WithMessage("Danh mục không được vượt quá 100 ký tự");

        RuleFor(x => x.Tags)
            .MaximumLength(500).WithMessage("Tags không được vượt quá 500 ký tự")
            .When(x => !string.IsNullOrWhiteSpace(x.Tags));
    }

    private bool BeValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
               (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }
}

