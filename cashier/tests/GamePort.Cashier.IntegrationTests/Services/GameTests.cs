using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Application.UseCases.Customers;
using GamePort.Cashier.Application.UseCases.Devices;
using GamePort.Cashier.Application.UseCases.Games;
using GamePort.Cashier.Application.UseCases.Sessions;
using GamePort.Cashier.Contracts.Hub;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Infrastructure.Data;
using GamePort.Cashier.Infrastructure.Repositories;
using GamePort.Cashier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GamePort.Cashier.IntegrationTests.Services;

public class GameTests
{
    private sealed class RecordingCommandSender : IDeviceCommandSender
    {
        public List<(Guid DeviceId, DeviceCommandMessage Command)> Sent { get; } = new();

        public Task<bool> SendAsync(Guid deviceId, DeviceCommandMessage command, CancellationToken cancellationToken = default)
        {
            Sent.Add((deviceId, command));
            return Task.FromResult(true);
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public CashierDbContext Context { get; }
        public DeviceRepository Devices { get; }
        public CustomerRepository Customers { get; }
        public SessionRepository Sessions { get; }
        public PricingRuleRepository Pricing { get; }
        public GameRepository Games { get; }
        public DeviceGameRepository DeviceGames { get; }
        public AuditLogRepository AuditLogs { get; }
        public OutboxRepository Outbox { get; }
        public UnitOfWork UnitOfWork { get; }
        public RecordingCommandSender CommandSender { get; } = new();

        private Fixture(CashierDbContext context)
        {
            Context = context;
            Devices = new DeviceRepository(context);
            Customers = new CustomerRepository(context);
            Sessions = new SessionRepository(context);
            Pricing = new PricingRuleRepository(context);
            Games = new GameRepository(context);
            DeviceGames = new DeviceGameRepository(context);
            AuditLogs = new AuditLogRepository(context);
            Outbox = new OutboxRepository(context);
            UnitOfWork = new UnitOfWork(context);
        }

        public static Fixture Create()
        {
            var options = new DbContextOptionsBuilder<CashierDbContext>()
                .UseInMemoryDatabase($"wingport-game-{Guid.NewGuid():N}")
                .Options;

            return new Fixture(new CashierDbContext(options));
        }

        public async Task<Customer> CreateCustomerAsync()
        {
            var handler = new RegisterCustomerHandler(Customers, new WalletRepository(Context), AuditLogs, Outbox, UnitOfWork);
            var result = await handler.HandleAsync(new RegisterCustomerCommand("Ali", null));
            return result.Value!.Customer;
        }

        public async Task<Device> CreateDeviceAsync(DeviceType type = DeviceType.GamingPC, string name = "PC-01")
        {
            return await Devices.CreateAsync(new Device { Name = name, Type = type, Status = DeviceStatus.Available });
        }

        public async Task<Game> CreateGameAsync(DeviceType type = DeviceType.GamingPC, string name = "Test Game")
        {
            var handler = new CreateGameHandler(Games, AuditLogs, Outbox, UnitOfWork);
            var result = await handler.HandleAsync(new CreateGameCommand(name, "1.0", @"C:\games\test.exe", null, type, null, "Cashier"));
            Assert.True(result.IsSuccess);
            return (await Games.GetByIdAsync(result.Value!.GameId))!;
        }

        public async Task StartSessionAsync(Guid customerId, Guid deviceId)
        {
            await Pricing.AddAsync(new PricingRule
            {
                Name = "hourly",
                DeviceType = DeviceType.GamingPC,
                PricePerHour = 100_000m,
                IsActive = true
            });

            var handler = new StartSessionHandler(Customers, Devices, Sessions, Pricing, new ReservationRepository(Context), AuditLogs, Outbox, UnitOfWork);
            var result = await handler.HandleAsync(new StartSessionCommand(customerId, deviceId, null, "Cashier"));
            Assert.True(result.IsSuccess);
        }

        public LaunchGameHandler LaunchHandler() => new(
            Devices, Games, DeviceGames, Sessions, CommandSender, AuditLogs, Outbox, UnitOfWork);

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    [Fact]
    public async Task AssignGame_FailsForMismatchedDeviceType()
    {
        await using var fixture = Fixture.Create();
        var device = await fixture.CreateDeviceAsync(DeviceType.GamingPC);
        var psGame = await fixture.CreateGameAsync(DeviceType.PlayStation, "PS Game");

        var handler = new AssignGameToDeviceHandler(
            fixture.DeviceGames, fixture.Devices, fixture.Games, fixture.AuditLogs, fixture.Outbox, fixture.UnitOfWork);

        var result = await handler.HandleAsync(new AssignGameToDeviceCommand(device.Id, psGame.Id, true, null, "Cashier"));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task LaunchGame_FailsWithoutOpenSession()
    {
        await using var fixture = Fixture.Create();
        var device = await fixture.CreateDeviceAsync();
        var game = await fixture.CreateGameAsync();
        await new AssignGameToDeviceHandler(
            fixture.DeviceGames, fixture.Devices, fixture.Games, fixture.AuditLogs, fixture.Outbox, fixture.UnitOfWork)
            .HandleAsync(new AssignGameToDeviceCommand(device.Id, game.Id, true, "1.0", "Cashier"));

        var result = await fixture.LaunchHandler().HandleAsync(new LaunchGameCommand(device.Id, game.Id, "Cashier"));

        Assert.False(result.IsSuccess);
        Assert.Empty(fixture.CommandSender.Sent);
    }

    [Fact]
    public async Task LaunchGame_FailsWithoutMapping()
    {
        await using var fixture = Fixture.Create();
        var customer = await fixture.CreateCustomerAsync();
        var device = await fixture.CreateDeviceAsync();
        var game = await fixture.CreateGameAsync();
        await fixture.StartSessionAsync(customer.Id, device.Id);

        var result = await fixture.LaunchHandler().HandleAsync(new LaunchGameCommand(device.Id, game.Id, "Cashier"));

        Assert.False(result.IsSuccess);
        Assert.Empty(fixture.CommandSender.Sent);
    }

    [Fact]
    public async Task LaunchGame_SendsCommandAndSetsCurrentGame()
    {
        await using var fixture = Fixture.Create();
        var customer = await fixture.CreateCustomerAsync();
        var device = await fixture.CreateDeviceAsync();
        var game = await fixture.CreateGameAsync();
        await fixture.StartSessionAsync(customer.Id, device.Id);
        await new AssignGameToDeviceHandler(
            fixture.DeviceGames, fixture.Devices, fixture.Games, fixture.AuditLogs, fixture.Outbox, fixture.UnitOfWork)
            .HandleAsync(new AssignGameToDeviceCommand(device.Id, game.Id, true, "1.0", "Cashier"));

        var result = await fixture.LaunchHandler().HandleAsync(new LaunchGameCommand(device.Id, game.Id, "Cashier"));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Delivered);
        Assert.Contains(fixture.CommandSender.Sent, s => s.Command.CommandType == DeviceCommandType.LaunchGame);

        var storedDevice = await fixture.Devices.GetByIdAsync(device.Id);
        Assert.Equal(game.Id, storedDevice!.CurrentGameId);
    }

    [Fact]
    public async Task CloseGame_ClearsCurrentGameAndSendsCommand()
    {
        await using var fixture = Fixture.Create();
        var device = await fixture.CreateDeviceAsync();
        device.CurrentGameId = Guid.NewGuid();
        await fixture.Devices.UpdateAsync(device);

        var handler = new CloseGameHandler(fixture.Devices, fixture.CommandSender, fixture.AuditLogs, fixture.Outbox, fixture.UnitOfWork);
        var result = await handler.HandleAsync(new CloseGameCommand(device.Id, "Cashier"));

        Assert.True(result.IsSuccess);
        Assert.Contains(fixture.CommandSender.Sent, s => s.Command.CommandType == DeviceCommandType.CloseGame);

        var storedDevice = await fixture.Devices.GetByIdAsync(device.Id);
        Assert.Null(storedDevice!.CurrentGameId);
    }
}
