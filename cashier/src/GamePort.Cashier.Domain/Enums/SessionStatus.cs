namespace GamePort.Cashier.Domain.Enums;

public enum SessionStatus
{
    Reserved = 1,
    Active = 2,
    Paused = 3,
    Completed = 4,
    Cancelled = 5,
    Expired = 6,
    Interrupted = 7
}
