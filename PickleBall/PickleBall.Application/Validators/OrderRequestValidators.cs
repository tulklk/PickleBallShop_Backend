using FluentValidation;
using PickleBall.Application.DTOs.Orders;

namespace PickleBall.Application.Validators;

public class ShippingAddressDtoValidator : AbstractValidator<ShippingAddressDto>
{
    public ShippingAddressDtoValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .MaximumLength(20)
            .Matches(@"^[0-9+\-\s()]{8,20}$").WithMessage("Số điện thoại không hợp lệ");
        RuleFor(x => x.AddressLine).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Ward).MaximumLength(100);
        RuleFor(x => x.District).MaximumLength(100);
        RuleFor(x => x.City).MaximumLength(100);
    }
}

public class GuestCartItemDtoValidator : AbstractValidator<GuestCartItemDto>
{
    public GuestCartItemDtoValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.ProductVariantId).NotEqual(Guid.Empty).When(x => x.ProductVariantId.HasValue);
        // Quantity must be a positive, bounded integer. Negative values would reduce the order total
        // and increase stock; huge values are an abuse vector.
        RuleFor(x => x.Quantity).InclusiveBetween(1, 100).WithMessage("Số lượng phải từ 1 đến 100");
    }
}

public class CreateGuestOrderRequestValidator : AbstractValidator<CreateGuestOrderRequest>
{
    public CreateGuestOrderRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.ShippingAddress).NotNull().SetValidator(new ShippingAddressDtoValidator()!);
        RuleFor(x => x.PaymentMethod).IsInEnum();
        RuleFor(x => x.VoucherCode).MaximumLength(100);
        RuleFor(x => x.Items)
            .NotNull()
            .Must(i => i != null && i.Count >= 1 && i.Count <= 50)
            .WithMessage("Giỏ hàng phải có từ 1 đến 50 sản phẩm");
        RuleForEach(x => x.Items).SetValidator(new GuestCartItemDtoValidator());
    }
}

public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.ShippingAddress).NotNull().SetValidator(new ShippingAddressDtoValidator()!);
        RuleFor(x => x.PaymentMethod).IsInEnum();
        RuleFor(x => x.VoucherCode).MaximumLength(100);
    }
}

public class ApplyVoucherForGuestOrderRequestValidator : AbstractValidator<ApplyVoucherForGuestOrderRequest>
{
    public ApplyVoucherForGuestOrderRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Items)
            .NotNull()
            .Must(i => i != null && i.Count >= 1 && i.Count <= 50)
            .WithMessage("Giỏ hàng phải có từ 1 đến 50 sản phẩm");
        RuleForEach(x => x.Items).SetValidator(new GuestCartItemDtoValidator());
    }
}

public class ApplyVoucherForCartRequestValidator : AbstractValidator<ApplyVoucherForCartRequest>
{
    public ApplyVoucherForCartRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
    }
}
