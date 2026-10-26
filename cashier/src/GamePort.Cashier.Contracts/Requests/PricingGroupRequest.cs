namespace GamePort.Cashier.Contracts.Requests;

public class PricingGroupRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // NULL = any device type; "GamingPC" = PC only; "PlayStation" = PS5 only
    public string? DeviceType { get; set; }
}
