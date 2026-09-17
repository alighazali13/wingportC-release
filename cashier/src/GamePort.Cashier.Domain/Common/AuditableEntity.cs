namespace GamePort.Cashier.Domain.Common;

public class AuditableEntity : BaseEntity
{
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}
