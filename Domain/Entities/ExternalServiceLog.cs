namespace Domain.Entities;

public class ExternalServiceLog
{
    public int Id { get; private set; }
    public Guid TransactionId { get; private set; }
    public string ServiceName { get; private set; } = string.Empty;
    public string Request { get; private set; } = string.Empty;
    public string? Response { get; private set; }
    public int? HttpStatusCode { get; private set; }
    public long DurationMs { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ExternalServiceLog() { }

    public ExternalServiceLog(Guid transactionId, string serviceName, string request,
                              string? response, int? httpStatusCode, long durationMs)
    {
        TransactionId = transactionId;
        ServiceName = serviceName;
        Request = request;
        Response = response;
        HttpStatusCode = httpStatusCode;
        DurationMs = durationMs;
        CreatedAt = DateTime.UtcNow;
    }
}