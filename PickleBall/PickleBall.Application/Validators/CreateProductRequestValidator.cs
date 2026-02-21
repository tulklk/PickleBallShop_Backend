using FluentValidation;
using PickleBall.Application.DTOs.Products;

namespace PickleBall.Application.Validators;

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(256).WithMessage("Name must not exceed 256 characters");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Category ID is required");

        RuleFor(x => x)
            .Must(x => x.Price > 0 || (x.OriginalPrice.HasValue && x.OriginalPrice.Value > 0))
            .WithMessage("Either Price (Giá giảm) or Original Price (Giá gốc) must be greater than 0");
        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Price must be greater than or equal to 0");
        RuleFor(x => x.OriginalPrice)
            .GreaterThanOrEqualTo(0).When(x => x.OriginalPrice.HasValue).WithMessage("Original price must be greater than or equal to 0");

        RuleFor(x => x.StockQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("Stock quantity must be greater than or equal to 0");

        RuleFor(x => x.Description)
            .MaximumLength(5000).WithMessage("Description must not exceed 5000 characters");

        RuleFor(x => x.Brand)
            .MaximumLength(256).WithMessage("Brand must not exceed 256 characters");
    }
}

