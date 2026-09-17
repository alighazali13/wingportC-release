namespace GamePort.Cashier.Contracts.Requests;

public class CreatePricingRuleRequest
{
    public string Name { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public decimal PricePerHour { get; set; }
}
