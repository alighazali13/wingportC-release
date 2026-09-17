using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Reports;

public record FinancialTransactionResult(
    Guid Id,
    DateTime CreatedAt,
    string Type,
    decimal Amount,
    decimal BalanceAfter,
    string? CustomerName,
    string? DeviceType,
    string? PaymentMethod,
    string? Description);

public record FinancialReportResult(
    DateTime From,
    DateTime To,
    decimal ServiceRevenue,
    decimal PcRevenue,
    decimal PsRevenue,
    decimal WalletTopUps,
    decimal CashCollected,
    int SessionCount,
    int TransactionCount,
    IReadOnlyList<FinancialTransactionResult> Transactions);

public class GetFinancialReportHandler
{
    private const int MaxTransactions = 100;

    private readonly IWalletRepository _wallets;
    private readonly IPaymentRepository _payments;
    private readonly ICustomerRepository _customers;
    private readonly ISessionRepository _sessions;

    public GetFinancialReportHandler(
        IWalletRepository wallets,
        IPaymentRepository payments,
        ICustomerRepository customers,
        ISessionRepository sessions)
    {
        _wallets = wallets;
        _payments = payments;
        _customers = customers;
        _sessions = sessions;
    }

    public async Task<FinancialReportResult> HandleAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        if (to <= from)
        {
            to = from.AddDays(1);
        }

        var transactions = (await _wallets.GetTransactionsByDateRangeAsync(from, to)).ToList();
        var payments = (await _payments.GetByDateRangeAsync(from, to)).ToList();

        var wallets = await _wallets.GetAllAsync();
        var walletToCustomer = wallets.ToDictionary(w => w.Id, w => w.CustomerId);

        var customers = await _customers.GetAllAsync();
        var customerNames = customers.ToDictionary(c => c.Id, c => c.Name);

        var sessionIds = transactions
            .Where(t => t.RelatedEntityType == nameof(GamePort.Cashier.Domain.Entities.Session) && t.RelatedEntityId.HasValue)
            .Select(t => t.RelatedEntityId!.Value)
            .Distinct()
            .ToList();

        var sessions = await _sessions.GetByIdsAsync(sessionIds);
        var sessionDeviceTypes = sessions.ToDictionary(s => s.Id, s => s.DeviceType);

        decimal serviceRevenue = 0m;
        decimal pcRevenue = 0m;
        decimal psRevenue = 0m;
        decimal walletTopUps = 0m;

        foreach (var transaction in transactions)
        {
            var amount = Math.Abs(transaction.Amount);

            if (transaction.Type == WalletTransactionTypes.SessionCharge)
            {
                serviceRevenue += amount;

                if (transaction.RelatedEntityId.HasValue &&
                    sessionDeviceTypes.TryGetValue(transaction.RelatedEntityId.Value, out var deviceType))
                {
                    if (deviceType == DeviceType.PlayStation)
                    {
                        psRevenue += amount;
                    }
                    else
                    {
                        pcRevenue += amount;
                    }
                }
            }
            else if (transaction.Type == WalletTransactionTypes.Recharge)
            {
                walletTopUps += amount;
            }
        }

        var cashCollected = payments
            .Where(p => p.Method == PaymentMethod.Cash)
            .Sum(p => p.Amount);

        var items = transactions
            .Take(MaxTransactions)
            .Select(t =>
            {
                string? customerName = null;
                if (walletToCustomer.TryGetValue(t.WalletId, out var customerId))
                {
                    customerNames.TryGetValue(customerId, out customerName);
                }

                string? deviceType = null;
                if (t.RelatedEntityId.HasValue &&
                    sessionDeviceTypes.TryGetValue(t.RelatedEntityId.Value, out var dt))
                {
                    deviceType = dt.ToString();
                }

                return new FinancialTransactionResult(
                    t.Id, t.CreatedAt, t.Type, t.Amount, t.BalanceAfter,
                    customerName, deviceType, null, t.Description);
            })
            .ToList();

        return new FinancialReportResult(
            from,
            to,
            serviceRevenue,
            pcRevenue,
            psRevenue,
            walletTopUps,
            cashCollected,
            sessionIds.Count,
            transactions.Count + payments.Count,
            items);
    }
}
