using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Wallets;

public record RechargeWalletCommand(
    Guid CustomerId,
    decimal Amount,
    PaymentMethod Method,
    string? OperatorInfo,
    string? IdempotencyKey);

public record RechargeWalletResult(
    Guid WalletId,
    decimal Balance,
    Guid? PaymentId,
    bool AlreadyApplied);
