using Application.Transfers.Common;
using MediatR;

namespace Application.Transfers.Queries.GetTransferById;


public  record GetTransferByIdQuery (int Id ): IRequest<TransferDto>;

