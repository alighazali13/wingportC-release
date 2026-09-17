using System.Text.Json;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Sync;

public class CustomerUpsertPayload
{
    public Guid? CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
}

public class CustomerUpsertMessageHandler : ICloudMessageHandler
{
    public const string Type = "customer.upsert";

    private readonly ICustomerRepository _customers;
    private readonly IWalletRepository _wallets;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IUnitOfWork _unitOfWork;

    public CustomerUpsertMessageHandler(
        ICustomerRepository customers,
        IWalletRepository wallets,
        IAuditLogRepository auditLogs,
        IUnitOfWork unitOfWork)
    {
        _customers = customers;
        _wallets = wallets;
        _auditLogs = auditLogs;
        _unitOfWork = unitOfWork;
    }

    public string MessageType => Type;

    public async Task HandleAsync(string payload, CancellationToken cancellationToken = default)
    {
        var data = JsonSerializer.Deserialize<CustomerUpsertPayload>(payload, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (data is null || string.IsNullOrWhiteSpace(data.Name))
        {
            throw new InvalidOperationException("Invalid customer.upsert payload.");
        }

        var existing = string.IsNullOrWhiteSpace(data.PhoneNumber)
            ? null
            : await _customers.GetByPhoneNumberAsync(data.PhoneNumber);

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (existing is not null)
            {
                existing.Name = data.Name;
                await _customers.UpdateAsync(existing);
            }
            else
            {
                var customer = new Customer
                {
                    Name = data.Name,
                    PhoneNumber = data.PhoneNumber,
                    MaxConcurrentSessions = 1
                };

                await _customers.CreateAsync(customer);
                await _wallets.CreateAsync(new Wallet { CustomerId = customer.Id, Balance = 0m });
                existing = customer;
            }

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "SyncCustomerUpsert",
                EntityType = nameof(Customer),
                EntityId = existing.Id,
                ActorType = AuditActorType.System,
                ActorName = "CloudSync",
                Source = "Cloud",
                AfterState = JsonSerializer.Serialize(new { existing.Name, existing.PhoneNumber })
            });
        }, cancellationToken);
    }
}
