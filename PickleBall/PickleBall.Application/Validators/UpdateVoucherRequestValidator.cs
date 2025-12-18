using FluentValidation;
using PickleBall.Application.DTOs.Vouchers;
using PickleBall.Domain.Entities;

namespace PickleBall.Application.Validators;

public class UpdateVoucherRequestValidator : AbstractValidator<UpdateVoucherRequest>
{
    public UpdateVoucherRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên mã giảm giá là bắt buộc")
            .MaximumLength(256);

        RuleFor(x => x.DiscountValue)
            .GreaterThan(0).WithMessage("Giá trị giảm phải lớn hơn 0");

        When(x => x.DiscountType == VoucherDiscountType.Percent, () =>
        {
            RuleFor(x => x.DiscountValue)
                .LessThanOrEqualTo(100).WithMessage("Giảm theo % phải nhỏ hơn hoặc bằng 100");
        });

        RuleFor(x => x.StartDate)
            .LessThan(x => x.EndDate)
            .WithMessage("Ngày bắt đầu phải trước ngày kết thúc");

        When(x => x.MinOrderAmount.HasValue, () =>
        {
            RuleFor(x => x.MinOrderAmount!.Value)
                .GreaterThan(0).WithMessage("Giá trị đơn hàng tối thiểu phải lớn hơn 0");
        });
    }
}

