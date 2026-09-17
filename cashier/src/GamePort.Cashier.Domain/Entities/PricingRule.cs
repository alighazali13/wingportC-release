using GamePort.Cashier.Domain.Common;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Domain.Entities;

public class PricingRule : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public DeviceType DeviceType { get; set; }
    public decimal PricePerHour { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
}
