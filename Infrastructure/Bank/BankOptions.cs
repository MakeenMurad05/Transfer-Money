namespace Infrastructure.Bank;

public class BankOptions
{
    public const string SectionName = "Bank";

    public string BaseUrl { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; }
}