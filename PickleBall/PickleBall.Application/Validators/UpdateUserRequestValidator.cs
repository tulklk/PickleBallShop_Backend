using FluentValidation;
using PickleBall.Application.DTOs.Users;

namespace PickleBall.Application.Validators;

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required")
            .MaximumLength(256).WithMessage("Full name must not exceed 256 characters");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(20).WithMessage("Phone number must not exceed 20 characters")
            .Matches(@"^[+]?[(]?[0-9]{1,4}[)]?[-\s.]?[(]?[0-9]{1,4}[)]?[-\s.]?[0-9]{1,9}$")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber))
            .WithMessage("Invalid phone number format");

        // Avatar is rendered by clients; only plain http(s) URLs (no javascript:/data: schemes).
        RuleFor(x => x.Avatar)
            .MaximumLength(2048)
            .Must(a => Uri.TryCreate(a, UriKind.Absolute, out var u) &&
                       (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps))
            .When(x => !string.IsNullOrWhiteSpace(x.Avatar))
            .WithMessage("Avatar must be an http(s) URL");
    }
}

