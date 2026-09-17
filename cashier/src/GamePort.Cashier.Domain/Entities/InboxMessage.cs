using GamePort.Cashier.Domain.Common;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Domain.Entities;

public class InboxMessage : BaseEntity
{
    public string ExternalId { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public InboxStatus Status { get; set; } = InboxStatus.Pending;
    public DateTime? ProcessedAt { get; set; }
    public string? LastError { get; set; }
}
