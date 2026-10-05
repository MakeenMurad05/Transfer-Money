using FluentValidation;

namespace Application.Transfers.Commands.CreateTransfer;

public class CreateTransferCommandValidator : AbstractValidator<CreateTransferCommand>
{

    public CreateTransferCommandValidator()
    {

        RuleFor(x => x.AccountNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(20)
            .Matches("^[0-9]+$").WithMessage("Account number must contain digits only.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .PrecisionScale(18, 3, true);

        RuleFor(x => x.Currency)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Length(3)
            .Matches("^[A-Z]{3}$").WithMessage("Currency must be 3 uppercase letters , e.g .LYD .");

        RuleFor(x => x.Reference)
            .NotEmpty()
            .MaximumLength(50);


    }




}