namespace Application.Transfers.Common;


public record TransferDto(
    int Id,
    Guid TransactionId,
    string Reference,
    string AccountNumber,
    decimal Amount,
    string Currency,
    string Status,
    string? BankReference,
    string? ResponseCode,
    string? ResponseMessage,
    DateTime CreatedAt);