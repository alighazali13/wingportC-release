using GamePort.Cashier.Domain.Common;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Domain.Entities;

public class OutboxEvent : BaseEntity
{
    public long Sequence { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;
    public int Attempts { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? LastError { get; set; }
}
