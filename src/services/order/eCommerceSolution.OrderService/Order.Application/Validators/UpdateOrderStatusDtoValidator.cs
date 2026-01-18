using FluentValidation;
using Order.Application.DTOs;

namespace OrderService.Application.Validators;

public class UpdateOrderStatusDtoValidator : AbstractValidator<UpdateOrderStatusDto>
{
    public UpdateOrderStatusDtoValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid order status");
    }
}