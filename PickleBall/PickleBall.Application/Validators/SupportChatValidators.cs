using FluentValidation;
using PickleBall.Application.DTOs.SupportChat;

namespace PickleBall.Application.Validators;

public class SendSupportMessageRequestValidator : AbstractValidator<SendSupportMessageRequest>
{
    public SendSupportMessageRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Message content is required")
            .MaximumLength(4000).WithMessage("Message content cannot exceed 4000 characters");
    }
}

public class SupportConversationQueryValidator : AbstractValidator<SupportConversationQuery>
{
    public SupportConversationQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0).WithMessage("Page must be greater than 0");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100");
    }
}

public class SupportMessageQueryValidator : AbstractValidator<SupportMessageQuery>
{
    public SupportMessageQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0).WithMessage("Page must be greater than 0");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 200).WithMessage("PageSize must be between 1 and 200");
    }
}
