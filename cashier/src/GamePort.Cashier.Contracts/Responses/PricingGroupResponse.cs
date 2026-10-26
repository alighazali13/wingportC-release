namespace GamePort.Cashier.Contracts.Responses;

public class PricingGroupResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DeviceType { get; set; }
    public bool IsActive { get; set; }
}
