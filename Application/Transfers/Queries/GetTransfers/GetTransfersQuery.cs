using Application.Transfers.Common;
using MediatR;

namespace Application.Transfers.Queries.GetTransfers;


public record GetTransfersQuery : IRequest<IReadOnlyList<TransferDto>>;

