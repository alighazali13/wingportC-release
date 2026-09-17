using GamePort.Cashier.Domain.Common;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Domain.Entities;

public class Session : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Guid DeviceId { get; set; }
    public DeviceType DeviceType { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? PlannedEndTime { get; set; }
    public DateTime? ActualEndTime { get; set; }
    public DateTime? PausedAt { get; set; }
    public long TotalPausedSeconds { get; set; }
    public DateTime? LastChargedAt { get; set; }
    public decimal TotalCharged { get; set; }
    public decimal PriceAtStart { get; set; }
    public SessionStatus Status { get; set; }
    public string? CreationSource { get; set; }
    public string? OperatorInfo { get; set; }
    public Customer Customer { get; set; } = null!;
    public Device Device { get; set; } = null!;
}
