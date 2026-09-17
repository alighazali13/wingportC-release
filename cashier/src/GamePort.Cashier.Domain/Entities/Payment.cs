using GamePort.Cashier.Domain.Common;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Domain.Entities;

public class Payment : BaseEntity
{
    public Guid? CustomerId { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? ReservationId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentType Type { get; set; } = PaymentType.Payment;
    public Guid? RelatedPaymentId { get; set; }
    public string? Description { get; set; }
    public Guid? OperatorId { get; set; }
    public string? OperatorInfo { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}
