using System.Text.Json;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.Services;

public class WalletService : IWalletService
{
    private readonly IWalletRepository _wallets;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public WalletService(
        IWalletRepository wallets,
        IAuditLogRepository auditLogs,
        IOutboxRepository outbox,
        IUnitOfWork unitOfWork)
    {
        _wallets = wallets;
        _auditLogs = auditLogs;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    public async Task<decimal> GetBalanceAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var wallet = await _wallets.GetByCustomerIdAsync(customerId);
        return wallet?.Balance ?? 0m;
    }

    public Task<Result<WalletOperationResult>> RechargeAsync(
        Guid customerId,
        decimal amount,
        string? description,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return ApplyAsync(
            customerId,
            amount,
            WalletTransactionTypes.Recharge,
            description,
            relatedEntityId: null,
            relatedEntityType: null,
            idempotencyKey,
            "WalletRecharge",
            cancellationToken);
    }

    public Task<Result<WalletOperationResult>> ChargeAsync(
        Guid customerId,
        decimal amount,
        string type,
        string? description,
        Guid? relatedEntityId,
        string? relatedEntityType,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return ApplyAsync(
            customerId,
            -Math.Abs(amount),
            type,
            description,
            relatedEntityId,
            relatedEntityType,
            idempotencyKey,
            "WalletCharge",
            cancellationToken);
    }

    public Task<Result<WalletOperationResult>> RefundAsync(
        Guid customerId,
        decimal amount,
        string? description,
        Guid? relatedEntityId,
        string? relatedEntityType,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return ApplyAsync(
            customerId,
            Math.Abs(amount),
            WalletTransactionTypes.Refund,
            description,
            relatedEntityId,
            relatedEntityType,
            idempotencyKey,
            "WalletRefund",
            cancellationToken);
    }

    private async Task<Result<WalletOperationResult>> ApplyAsync(
        Guid customerId,
        decimal signedAmount,
        string type,
        string? description,
        Guid? relatedEntityId,
        string? relatedEntityType,
        string idempotencyKey,
        string auditAction,
        CancellationToken cancellationToken)
    {
        if (signedAmount == 0)
        {
            return Result<WalletOperationResult>.Failure("مبلغ تراکنش باید غیرصفر باشد.");
        }

        var existing = await _wallets.GetTransactionByIdempotencyKeyAsync(idempotencyKey);
        if (existing is not null)
        {
            var existingWallet = await _wallets.GetByIdAsync(existing.WalletId);
            return Result<WalletOperationResult>.Success(new WalletOperationResult(
                existing.WalletId,
                existingWallet?.CustomerId ?? customerId,
                existingWallet?.Balance ?? existing.BalanceAfter,
                existing.Amount,
                AlreadyApplied: true));
        }

        var wallet = await _wallets.GetByCustomerIdAsync(customerId);
        if (wallet is null)
        {
            return Result<WalletOperationResult>.Failure("کیف پول مشتری یافت نشد.");
        }

        if (wallet.Balance + signedAmount < 0)
        {
            return Result<WalletOperationResult>.Failure("موجودی کیف پول کافی نیست.");
        }

        var balanceBefore = wallet.Balance;
        var balanceAfter = balanceBefore + signedAmount;
        var transactionId = Guid.NewGuid();

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            wallet.Balance = balanceAfter;
            await _wallets.UpdateAsync(wallet);

            await _wallets.AddTransactionAsync(new WalletTransaction
            {
                Id = transactionId,
                WalletId = wallet.Id,
                Amount = signedAmount,
                BalanceAfter = balanceAfter,
                Type = type,
                Description = description,
                IdempotencyKey = idempotencyKey,
                RelatedEntityId = relatedEntityId,
                RelatedEntityType = relatedEntityType
            });

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = auditAction,
                EntityType = nameof(Wallet),
                EntityId = wallet.Id,
                ActorType = AuditActorType.Employee,
                Source = "Cashier",
                BeforeState = JsonSerializer.Serialize(new { Balance = balanceBefore }),
                AfterState = JsonSerializer.Serialize(new { Balance = balanceAfter, Amount = signedAmount, Type = type })
            });

            await _outbox.AddAsync(new OutboxEvent
            {
                EventType = "wallet.transaction",
                IdempotencyKey = $"wallet.transaction:{idempotencyKey}",
                Payload = JsonSerializer.Serialize(new
                {
                    TransactionId = transactionId,
                    WalletId = wallet.Id,
                    CustomerId = wallet.CustomerId,
                    Amount = signedAmount,
                    BalanceAfter = balanceAfter,
                    Type = type,
                    relatedEntityId,
                    relatedEntityType
                })
            });
        }, cancellationToken);

        return Result<WalletOperationResult>.Success(new WalletOperationResult(
            wallet.Id, wallet.CustomerId, balanceAfter, signedAmount, AlreadyApplied: false));
    }
}
