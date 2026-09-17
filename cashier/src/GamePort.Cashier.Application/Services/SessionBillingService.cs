using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.Services;

public record SessionBillingOutcome(
    decimal AmountCharged,
    bool InsufficientFunds);

public class SessionBillingService
{
    private readonly ISessionRepository _sessions;
    private readonly IWalletService _walletService;
    private readonly IUnitOfWork _unitOfWork;

    public SessionBillingService(
        ISessionRepository sessions,
        IWalletService walletService,
        IUnitOfWork unitOfWork)
    {
        _sessions = sessions;
        _walletService = walletService;
        _unitOfWork = unitOfWork;
    }

    public static decimal CalculateCharge(decimal pricePerHour, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return 0m;
        }

        return Math.Round(pricePerHour * (decimal)duration.TotalHours, 2, MidpointRounding.AwayFromZero);
    }

    public async Task<SessionBillingOutcome> ChargeUpToAsync(Session session, DateTime upTo, CancellationToken cancellationToken = default)
    {
        var from = session.LastChargedAt ?? session.StartTime;
        if (upTo <= from)
        {
            return new SessionBillingOutcome(0m, false);
        }

        var amount = CalculateCharge(session.PriceAtStart, upTo - from);
        if (amount <= 0)
        {
            await AdvanceAsync(session, upTo, 0m, cancellationToken);
            return new SessionBillingOutcome(0m, false);
        }

        var balance = await _walletService.GetBalanceAsync(session.CustomerId, cancellationToken);
        var insufficientFunds = false;
        var amountToCharge = amount;

        if (amountToCharge > balance)
        {
            amountToCharge = balance;
            insufficientFunds = true;
        }

        if (amountToCharge > 0)
        {
            var idempotencyKey = $"session.charge:{session.Id}:{from.Ticks}";
            var chargeResult = await _walletService.ChargeAsync(
                session.CustomerId,
                amountToCharge,
                WalletTransactionTypes.SessionCharge,
                $"شارژ نشست {session.Id}",
                session.Id,
                nameof(Session),
                idempotencyKey,
                cancellationToken);

            if (!chargeResult.IsSuccess)
            {
                return new SessionBillingOutcome(0m, true);
            }
        }

        await AdvanceAsync(session, upTo, amountToCharge, cancellationToken);
        return new SessionBillingOutcome(amountToCharge, insufficientFunds);
    }

    private async Task AdvanceAsync(Session session, DateTime upTo, decimal chargedAmount, CancellationToken cancellationToken)
    {
        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            session.LastChargedAt = upTo;
            session.TotalCharged += chargedAmount;
            await _sessions.UpdateAsync(session);
        }, cancellationToken);
    }
}
