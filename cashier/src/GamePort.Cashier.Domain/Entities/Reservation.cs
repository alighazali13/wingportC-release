using GamePort.Cashier.Domain.Common;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Domain.Entities;

public class Reservation : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Guid DeviceId { get; set; }
    public DeviceType DeviceType { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Reserved;
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public Guid? ConvertedSessionId { get; set; }
    public DateTime? ConvertedAt { get; set; }
    public Customer Customer { get; set; } = null!;
    public Device Device { get; set; } = null!;
}
