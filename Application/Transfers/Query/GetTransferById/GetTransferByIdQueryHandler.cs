using Application.Common;
using Application.Common.Interfaces;
using Application.Transfers.Common;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Transfers.Query.GetTransfers;


public class GetTransferByIdQueryHandler : IRequestHandler<GetTransferByIdQuery, TransferDto>
{

    private readonly IAppDbContext _context ;

    public GetTransferByIdQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<TransferDto> Handle(GetTransferByIdQuery request, CancellationToken cancellationToken)
    {
         var transfer =  await _context.Transfers
                .AsNoTracking()
                .Where(t => t.Id == request.Id)
                .Select(TransferMappings.ToDtoExpression)
                .FirstOrDefaultAsync(cancellationToken);

                return transfer ?? throw new NotFoundException($"Transfer with id {request.Id} was not found.");
    }
}