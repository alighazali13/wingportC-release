using GamePort.Cashier.Domain.Common;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Domain.Entities;

public class AuditLog : BaseEntity
{
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public Guid? ActorId { get; set; }
    public AuditActorType ActorType { get; set; } = AuditActorType.System;
    public string? ActorName { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? BeforeState { get; set; }
    public string? AfterState { get; set; }
    public Guid? CorrelationId { get; set; }
    public Guid? DeviceId { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? Reason { get; set; }
}
