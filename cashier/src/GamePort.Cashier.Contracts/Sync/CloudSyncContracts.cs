namespace GamePort.Cashier.Contracts.Sync;

public class CloudSyncEventDto
{
    public long Sequence { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}

public class CloudPushRequest
{
    public List<CloudSyncEventDto> Events { get; set; } = new();
}

public class CloudPushResponse
{
    public bool Accepted { get; set; }
    public long? Cursor { get; set; }
    public string? Error { get; set; }
}

public class CloudChangeDto
{
    public long Cursor { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
}

public class CloudPullResponse
{
    public long NextCursor { get; set; }
    public List<CloudChangeDto> Changes { get; set; } = new();
}
