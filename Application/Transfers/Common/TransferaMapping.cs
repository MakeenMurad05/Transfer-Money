using System.Linq.Expressions;
using Domain.Entities;

namespace Application.Transfers.Common;

public static class TransferMappings
{
    // EF Core reads this and turns it into SQL
    public static readonly Expression<Func<Transfer, TransferDto>> ToDtoExpression = t => new TransferDto(
        t.Id,
        t.TransactionId,
        t.Reference,
        t.AccountNumber,
        t.Amount,
        t.Currency,
        t.Status.ToString(),
        t.BankReference,
        t.ResponseCode,
        t.ResponseMessage,
        t.CreatedAt);

    // The same mapping, compiled once, for objects already in memory (used by the Create handler)
    private static readonly Func<Transfer, TransferDto> ToDtoFunc = ToDtoExpression.Compile();

    public static TransferDto ToDto(this Transfer transfer) => ToDtoFunc(transfer);
}