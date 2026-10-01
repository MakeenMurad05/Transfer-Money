using Application.Transfers.Common;
using MediatR;

namespace Application.Transfers.Query.GetTransfers ;


public  record GetTransfersQuery : IRequest<IReadOnlyList<TransferDto>>;

