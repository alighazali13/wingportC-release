namespace GamePort.Cashier.Contracts.Requests;

public class ReservationIdRequest
{
    public Guid ReservationId { get; set; }
    public string? Reason { get; set; }
}
