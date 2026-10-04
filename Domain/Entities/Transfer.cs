using Domain.Enums;

namespace Domain.Entities ;

public class Transfer
{
    
    public int Id { get; private set; }
    public Guid TransactionId { get; private set; }
    public string Reference { get; private set; } = string.Empty;
    public string AccountNumber { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public TransferStatus Status { get; private set; }

    public string? BankReference { get; private set; }
    public string? ResponseCode { get; private set; }
    public string? ResponseMessage { get; private set; }
    public DateTime CreatedAt { get; private set; }



    private Transfer () {}

    public Transfer(string reference, string accountNumber, decimal amount, string currency)
    {
        TransactionId = Guid.NewGuid();
        Reference = reference;
        AccountNumber = accountNumber;
        Amount = amount;
        Currency = currency;
        Status = TransferStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkSuccess(string? bankReference, string responseCode, string responseMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bankReference);
        EnsurePending();
        Status = TransferStatus.Success;
        BankReference = bankReference;
        ResponseCode = responseCode;
        ResponseMessage = responseMessage;
    }

    public void MarkFailed(string? responseCode, string responseMessage)
    {
        EnsurePending();
        Status = TransferStatus.Failed;
        ResponseCode = responseCode;
        ResponseMessage = responseMessage;
    }

    private void EnsurePending()
    {
        if (Status != TransferStatus.Pending)
            throw new InvalidOperationException("Only a pending transfer can be updated.");
    }



}