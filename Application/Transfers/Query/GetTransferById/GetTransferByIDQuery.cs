using Application.Transfers.Common;
using MediatR;

namespace Application.Transfers.Query.GetTransfers ;


public  record GetTransferByIdQuery (int Id ): IRequest<TransferDto>;

