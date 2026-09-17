using GamePort.Cashier.Application.Common;

namespace GamePort.Cashier.Application.Interfaces;

public record WalletOperationResult(
    Guid WalletId,
    Guid CustomerId,
    decimal Balance,
    decimal Amount,
    bool AlreadyApplied);

public interface IWalletService
{
    Task<decimal> GetBalanceAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<Result<WalletOperationResult>> RechargeAsync(
        Guid customerId,
        decimal amount,
        string? description,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<Result<WalletOperationResult>> ChargeAsync(
        Guid customerId,
        decimal amount,
        string type,
        string? description,
        Guid? relatedEntityId,
        string? relatedEntityType,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<Result<WalletOperationResult>> RefundAsync(
        Guid customerId,
        decimal amount,
        string? description,
        Guid? relatedEntityId,
        string? relatedEntityType,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}
