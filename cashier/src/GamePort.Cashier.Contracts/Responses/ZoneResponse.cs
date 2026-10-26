namespace GamePort.Cashier.Contracts.Responses;

public class ZoneResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public string? ParentName { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}
