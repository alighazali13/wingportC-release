using GamePort.Cashier.Domain.Common;

namespace GamePort.Cashier.Domain.Entities;

public class AttendanceRecord : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public DateTime ClockInAt { get; set; }
    public DateTime? ClockOutAt { get; set; }
    public string? Notes { get; set; }
    public Employee Employee { get; set; } = null!;
}
