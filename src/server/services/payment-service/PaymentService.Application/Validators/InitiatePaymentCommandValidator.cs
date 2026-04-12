using FluentValidation;
using PaymentService.Application.Commands.Payments;

//! other half of ValidationBehavior file -> defines rule
// FluentValidation -> is a library that lets you write validation rules

namespace PaymentService.Application.Validators;

public class InitiatePaymentCommandValidator : AbstractValidator<InitiatePaymentCommand>
{
    public InitiatePaymentCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();      // user must exist
        RuleFor(x => x.CardId).NotEmpty();      // card must be provided
        RuleFor(x => x.BillId).NotEmpty();      // payment must belong to a bill
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Payment amount must be greater than zero.");
        RuleFor(x => x.PaymentType).IsInEnum();     // must be validate enum
    }
}
