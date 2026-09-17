namespace GamePort.Cashier.Contracts.Requests;

public class RegisterCustomerRequest
{
    public string Name { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
}
