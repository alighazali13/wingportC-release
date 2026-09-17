using GamePort.Cashier.Domain.Common;

namespace GamePort.Cashier.Domain.Entities;

public class Wallet : BaseEntity
{
    public Guid CustomerId { get; set; }
    public decimal Balance { get; set; }
    public Customer Customer { get; set; } = null!;
    public ICollection<WalletTransaction> Transactions { get; set; } = new List<WalletTransaction>();
}
