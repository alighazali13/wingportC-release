using GamePort.Cashier.Domain.Entities;

namespace GamePort.Cashier.Application.UseCases.Customers;

public record RegisterCustomerCommand(
    string Name,
    string? PhoneNumber);

public record RegisterCustomerResult(
    Customer Customer,
    Wallet Wallet);
