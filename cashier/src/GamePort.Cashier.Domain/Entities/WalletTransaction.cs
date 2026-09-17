using GamePort.Cashier.Domain.Common;

namespace GamePort.Cashier.Domain.Entities;

public class WalletTransaction : BaseEntity
{
    public Guid WalletId { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IdempotencyKey { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public Wallet Wallet { get; set; } = null!;
}
