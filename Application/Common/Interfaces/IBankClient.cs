using Application.Common.Models;

namespace Application.Common.Interfaces;

public interface IBankClient
{
    Task<BankTransferResult> TransferAsync(BankTransferRequest request, CancellationToken cancellationToken);
}