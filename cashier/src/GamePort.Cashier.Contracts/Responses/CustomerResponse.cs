namespace GamePort.Cashier.Contracts.Responses;

public class CustomerResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public int MaxConcurrentSessions { get; set; }
    public decimal? WalletBalance { get; set; }
    public DateTime CreatedAt { get; set; }
}
