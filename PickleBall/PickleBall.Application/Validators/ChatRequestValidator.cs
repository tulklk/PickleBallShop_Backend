using FluentValidation;
using PickleBall.Application.DTOs.Chat;

namespace PickleBall.Application.Validators;

public class ChatRequestValidator : AbstractValidator<ChatRequest>
{
    public ChatRequestValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.History)
            .Must(h => h == null || h.Count <= 50)
            .WithMessage("History is too long");
        RuleForEach(x => x.History).ChildRules(h =>
        {
            h.RuleFor(m => m.Role).MaximumLength(20);
            h.RuleFor(m => m.Content).MaximumLength(4000);
        });
    }
}
