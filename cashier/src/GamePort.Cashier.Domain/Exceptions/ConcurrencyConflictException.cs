namespace GamePort.Cashier.Domain.Exceptions;

/// <summary>
/// Raised when an optimistic concurrency check fails (e.g. PostgreSQL xmin mismatch).
/// Translated from EF Core's DbUpdateConcurrencyException at the infrastructure layer.
/// </summary>
public class ConcurrencyConflictException : DomainException
{
    public ConcurrencyConflictException(string message) : base(message) { }
    public ConcurrencyConflictException(string message, Exception innerException) : base(message, innerException) { }
}
