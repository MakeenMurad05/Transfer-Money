namespace Application.Common.Models;

public record BankTransferResult(
    bool IsSuccess,
    string? ResponseCode,
    string ResponseMessage,
    string? BankReference,
    string RequestBody,
    string? ResponseBody,
    int? HttpStatusCode,
    long DurationMs);