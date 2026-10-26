using GamePort.Cashier.Domain.Common;

namespace GamePort.Cashier.Domain.Entities;

/// <summary>
/// Physical location for devices. Supports optional parent hierarchy
/// (e.g., Main Hall → VIP Section).
/// </summary>
public class Zone : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public Zone? Parent { get; set; }
}
