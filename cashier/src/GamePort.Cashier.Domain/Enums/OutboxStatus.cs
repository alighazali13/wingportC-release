namespace GamePort.Cashier.Domain.Enums;

public enum OutboxStatus
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4
}
