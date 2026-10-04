using Application.Transfers.Common;
using MediatR;

namespace Application.Transfers.Commands.CreateTransfer;

public record CreateTransferCommand(
    string AccountNumber,
    decimal Amount,
    string Currency,
    string Reference
): IRequest<TransferDto>;

