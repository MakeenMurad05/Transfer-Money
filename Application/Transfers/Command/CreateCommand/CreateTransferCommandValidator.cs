using FluentValidation;

namespace Transfers.Command.CreateCommand;

public class CreateTransferCommandValidator : AbstractValidator<CreateTransferCommand>
{
    
    public CreateTransferCommandValidator()
    {
        
        RuleFor(x => x.AccountNumber)
            .NotEmpty()
            .MaximumLength(20);

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .PrecisionScale(18, 3, true);

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3);

        RuleFor(x => x.Reference)
            .NotEmpty()
            .MaximumLength(50);


    }




}