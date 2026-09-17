using System.Text.Json;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Customers;

public class RegisterCustomerHandler
{
    private readonly ICustomerRepository _customers;
    private readonly IWalletRepository _wallets;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterCustomerHandler(
        ICustomerRepository customers,
        IWalletRepository wallets,
        IAuditLogRepository auditLogs,
        IOutboxRepository outbox,
        IUnitOfWork unitOfWork)
    {
        _customers = customers;
        _wallets = wallets;
        _auditLogs = auditLogs;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RegisterCustomerResult>> HandleAsync(RegisterCustomerCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Result<RegisterCustomerResult>.Failure("نام مشتری الزامی است.");
        }

        if (!string.IsNullOrWhiteSpace(command.PhoneNumber))
        {
            var existing = await _customers.GetByPhoneNumberAsync(command.PhoneNumber.Trim());
            if (existing is not null)
            {
                return Result<RegisterCustomerResult>.Failure("مشتری با این شماره تلفن قبلاً ثبت شده است.");
            }
        }

        var customer = new Customer
        {
            Name = command.Name.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(command.PhoneNumber) ? null : command.PhoneNumber.Trim(),
            MaxConcurrentSessions = 1
        };

        var wallet = new Wallet
        {
            CustomerId = customer.Id,
            Balance = 0m
        };

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await _customers.CreateAsync(customer);

            wallet.CustomerId = customer.Id;
            await _wallets.CreateAsync(wallet);

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "RegisterCustomer",
                EntityType = nameof(Customer),
                EntityId = customer.Id,
                ActorType = AuditActorType.Employee,
                Source = "Cashier",
                AfterState = JsonSerializer.Serialize(new { customer.Name, customer.PhoneNumber })
            });

            await _outbox.AddAsync(new OutboxEvent
            {
                EventType = "customer.registered",
                IdempotencyKey = $"customer.registered:{customer.Id}",
                Payload = JsonSerializer.Serialize(new
                {
                    CustomerId = customer.Id,
                    customer.Name,
                    customer.PhoneNumber
                })
            });
        }, cancellationToken);

        return Result<RegisterCustomerResult>.Success(new RegisterCustomerResult(customer, wallet));
    }
}
