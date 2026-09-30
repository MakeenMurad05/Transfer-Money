namespace Application.Common.Interfaces;

using Microsoft.EntityFrameworkCore;
using Domain.Entities;
public interface IAppDbContext
{
    DbSet<Transfer> Transfers { get; }
    DbSet<ExternalServiceLog> ExternalServiceLogs { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}