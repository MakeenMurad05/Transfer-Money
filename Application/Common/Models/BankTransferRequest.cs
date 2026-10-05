namespace Application.Common.Models;

public record BankTransferRequest(
    Guid TransactionId,
    string AccountNumber,
    decimal Amount,
    string Currency,
    string Reference);