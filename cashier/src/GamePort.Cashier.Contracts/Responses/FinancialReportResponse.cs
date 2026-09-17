namespace GamePort.Cashier.Contracts.Responses;

public class FinancialReportResponse
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public decimal ServiceRevenue { get; set; }
    public decimal PcRevenue { get; set; }
    public decimal PsRevenue { get; set; }
    public decimal WalletTopUps { get; set; }
    public decimal CashCollected { get; set; }
    public int SessionCount { get; set; }
    public int TransactionCount { get; set; }
    public List<FinancialTransactionDto> Transactions { get; set; } = new();
}

public class FinancialTransactionDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? CustomerName { get; set; }
    public string? DeviceType { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Description { get; set; }
}
