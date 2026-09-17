using GamePort.Cashier.Domain.Common;

namespace GamePort.Cashier.Domain.Entities;

public class Employee : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
}
