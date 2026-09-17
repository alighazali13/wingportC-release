namespace GamePort.Cashier.Contracts.Responses;

public class WalletResponse
{
    public Guid WalletId { get; set; }
    public Guid CustomerId { get; set; }
    public decimal Balance { get; set; }
}

public class WalletTransactionResponse
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public DateTime CreatedAt { get; set; }
}
