using GamePort.Cashier.Domain.Common;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Domain.Entities;

/// <summary>
/// A pricing category label assigned to a Device (e.g., "PC Standard", "PS5 VIP").
/// Not an independent pricing engine — actual rates live on PricingRule.
/// DeviceType: NULL = any device type; GamingPC = PC only; PlayStation = PS5 only.
/// </summary>
public class PricingGroup : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DeviceType? DeviceType { get; set; }
    public bool IsActive { get; set; } = true;
}
