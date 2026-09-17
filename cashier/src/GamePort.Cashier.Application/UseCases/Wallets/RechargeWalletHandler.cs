using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Wallets;

public class RechargeWalletHandler
{
    private readonly IWalletService _walletService;
    private readonly IPaymentRepository _payments;

    public RechargeWalletHandler(IWalletService walletService, IPaymentRepository payments)
    {
        _walletService = walletService;
        _payments = payments;
    }

    public async Task<Result<RechargeWalletResult>> HandleAsync(RechargeWalletCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Amount <= 0)
        {
            return Result<RechargeWalletResult>.Failure("مبلغ شارژ باید بزرگ‌تر از صفر باشد.");
        }

        if (command.Method == PaymentMethod.Wallet)
        {
            return Result<RechargeWalletResult>.Failure("شارژ کیف پول با خود کیف پول مجاز نیست.");
        }

        var idempotencyKey = string.IsNullOrWhiteSpace(command.IdempotencyKey)
            ? $"wallet.recharge:{command.CustomerId}:{Guid.NewGuid():N}"
            : command.IdempotencyKey;

        var recharge = await _walletService.RechargeAsync(
            command.CustomerId,
            command.Amount,
            $"شارژ کیف پول ({command.Method})",
            idempotencyKey,
            cancellationToken);

        if (!recharge.IsSuccess)
        {
            return Result<RechargeWalletResult>.Failure(recharge.Error!);
        }

        Guid? paymentId = null;
        if (!recharge.Value!.AlreadyApplied)
        {
            var payment = await _payments.AddAsync(new Payment
            {
                CustomerId = command.CustomerId,
                Amount = command.Amount,
                Method = command.Method,
                Type = PaymentType.Payment,
                Description = "شارژ کیف پول",
                OperatorInfo = command.OperatorInfo,
                IdempotencyKey = $"payment:{idempotencyKey}"
            });
            paymentId = payment.Id;
        }

        return Result<RechargeWalletResult>.Success(new RechargeWalletResult(
            recharge.Value.WalletId,
            recharge.Value.Balance,
            paymentId,
            recharge.Value.AlreadyApplied));
    }
}
