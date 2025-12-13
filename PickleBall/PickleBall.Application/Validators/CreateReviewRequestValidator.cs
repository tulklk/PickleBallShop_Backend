using FluentValidation;
using PickleBall.Application.DTOs.Reviews;

namespace PickleBall.Application.Validators;

public class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
{
    public CreateReviewRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên là bắt buộc")
            .MaximumLength(256).WithMessage("Họ tên không được vượt quá 256 ký tự");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Số điện thoại là bắt buộc")
            .MaximumLength(20).WithMessage("Số điện thoại không được vượt quá 20 ký tự")
            .Matches(@"^[0-9+\-\s()]+$").WithMessage("Số điện thoại không hợp lệ");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email không hợp lệ")
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .MaximumLength(256).WithMessage("Email không được vượt quá 256 ký tự");

        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 5).WithMessage("Đánh giá phải từ 1 đến 5 sao");

        RuleFor(x => x.Comment)
            .MinimumLength(10).WithMessage("Bình luận phải có ít nhất 10 ký tự")
            .When(x => !string.IsNullOrWhiteSpace(x.Comment))
            .MaximumLength(2000).WithMessage("Bình luận không được vượt quá 2000 ký tự");

        RuleFor(x => x.ReviewImageUrl)
            .Must(BeValidUrl).WithMessage("URL ảnh không hợp lệ")
            .When(x => !string.IsNullOrWhiteSpace(x.ReviewImageUrl))
            .MaximumLength(1000).WithMessage("URL ảnh không được vượt quá 1000 ký tự");
    }

    private bool BeValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return true;

        return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
               (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }
}

