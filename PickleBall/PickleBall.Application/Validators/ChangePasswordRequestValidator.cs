using FluentValidation;
using PickleBall.Application.DTOs.Users;

namespace PickleBall.Application.Validators;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required")
            .Must(p => PickleBall.Application.Security.PasswordPolicy.IsValid(p, out _))
            .WithMessage(PickleBall.Application.Security.PasswordPolicy.RequirementsMessage);

        RuleFor(x => x.ConfirmNewPassword)
            .NotEmpty().WithMessage("Password confirmation is required")
            .Equal(x => x.NewPassword)
            .WithMessage("Password confirmation must match the new password");
    }
}

