using Application.Transfers.Common;
using MediatR;

namespace Transfers.Command.CreateCommand;

public record CreateTransferCommand(
    string AccountNumber,
    decimal Amount,
    string Currency,
    string Reference
): IRequest<TransferDto>;

