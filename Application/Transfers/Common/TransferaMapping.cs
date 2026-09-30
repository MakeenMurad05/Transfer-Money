using Domain.Entities;

namespace Application.Transfers.Common;

public static class TransferMappings
{
    public static TransferDto ToDto(this Transfer transfer) => new(
        transfer.Id,
        transfer.TransactionId,
        transfer.Reference,
        transfer.AccountNumber,
        transfer.Amount,
        transfer.Currency,
        transfer.Status.ToString(),
        transfer.BankReference,
        transfer.ResponseCode,
        transfer.ResponseMessage,
        transfer.CreatedAt);
}