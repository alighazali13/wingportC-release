namespace GamePort.Cashier.Contracts.Requests;

public class ActivationRequest
{
    public bool IsActive { get; set; }
    public string Xmin { get; set; } = string.Empty;
}
