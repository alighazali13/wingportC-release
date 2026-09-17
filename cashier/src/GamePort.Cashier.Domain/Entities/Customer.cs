using GamePort.Cashier.Domain.Common;

namespace GamePort.Cashier.Domain.Entities;

public class Customer : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? OtpCode { get; set; }
    public DateTime? OtpExpiry { get; set; }
    public int MaxConcurrentSessions { get; set; } = 1;
    public Wallet? Wallet { get; set; }
}
