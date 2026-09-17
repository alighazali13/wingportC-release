namespace GamePort.Cashier.Contracts.Requests;

public class RechargeWalletRequest
{
    public Guid CustomerId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "Cash";
    public string? IdempotencyKey { get; set; }
}
