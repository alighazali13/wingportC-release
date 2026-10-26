namespace GamePort.Cashier.Contracts.Requests;

public class ZoneRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public int DisplayOrder { get; set; }
}
