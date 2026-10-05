using Application.Common.Interfaces;
using Application.Transfers.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Transfers.Queries.GetTransfers;


public class GetTransfersQueryHandler(IAppDbContext context) : IRequestHandler<GetTransfersQuery, IReadOnlyList<TransferDto>>
{

    private readonly IAppDbContext _context = context;

    public async Task<IReadOnlyList<TransferDto>> Handle(GetTransfersQuery request, CancellationToken cancellationToken)
    {

        return await _context.Transfers
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Select(TransferaMapping.ToDtoExpression)
            .ToListAsync(cancellationToken);

    }
}