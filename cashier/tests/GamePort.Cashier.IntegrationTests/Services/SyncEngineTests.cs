using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Application.UseCases.Sync;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Infrastructure.Data;
using GamePort.Cashier.Infrastructure.Repositories;
using GamePort.Cashier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GamePort.Cashier.IntegrationTests.Services;

public class SyncEngineTests
{
    private sealed class FakeCloudSyncClient : ICloudSyncClient
    {
        public bool IsConfigured { get; set; } = true;
        public bool PushSucceeds { get; set; } = true;
        public bool PullSucceeds { get; set; } = true;
        public string? Error { get; set; }
        public List<CloudSyncEvent> Pushed { get; } = new();
        public List<CloudChange> Changes { get; } = new();
        public long NextCursor { get; set; }

        public Task<CloudPushResult> PushAsync(IReadOnlyList<CloudSyncEvent> events, CancellationToken cancellationToken = default)
        {
            Pushed.AddRange(events);
            return Task.FromResult(PushSucceeds
                ? new CloudPushResult(true, null)
                : new CloudPushResult(false, Error ?? "cloud down"));
        }

        public Task<CloudPullResult> PullAsync(long cursor, int batchSize, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PullSucceeds
                ? new CloudPullResult(true, NextCursor, Changes.ToList(), null)
                : new CloudPullResult(false, cursor, Array.Empty<CloudChange>(), Error ?? "cloud down"));
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public CashierDbContext Context { get; }
        public OutboxRepository Outbox { get; }
        public InboxRepository Inbox { get; }
        public SyncCheckpointRepository Checkpoints { get; }
        public CustomerRepository Customers { get; }
        public WalletRepository Wallets { get; }
        public AuditLogRepository AuditLogs { get; }
        public UnitOfWork UnitOfWork { get; }
        public FakeCloudSyncClient Cloud { get; } = new();

        private Fixture(CashierDbContext context)
        {
            Context = context;
            Outbox = new OutboxRepository(context);
            Inbox = new InboxRepository(context);
            Checkpoints = new SyncCheckpointRepository(context);
            Customers = new CustomerRepository(context);
            Wallets = new WalletRepository(context);
            AuditLogs = new AuditLogRepository(context);
            UnitOfWork = new UnitOfWork(context);
        }

        public static Fixture Create()
        {
            var options = new DbContextOptionsBuilder<CashierDbContext>()
                .UseInMemoryDatabase($"wingport-sync-{Guid.NewGuid():N}")
                .Options;

            return new Fixture(new CashierDbContext(options));
        }

        public ProcessOutboxHandler OutboxHandler() => new(Outbox, Checkpoints, Cloud, UnitOfWork);

        public ProcessInboxHandler InboxHandler() => new(
            Inbox,
            Checkpoints,
            Cloud,
            new CloudMessageDispatcher(new ICloudMessageHandler[]
            {
                new CustomerUpsertMessageHandler(Customers, Wallets, AuditLogs, UnitOfWork)
            }));

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    [Fact]
    public async Task Outbox_PushesPendingEvents_AndAdvancesCheckpoint()
    {
        await using var fixture = Fixture.Create();
        await fixture.Outbox.AddAsync(new OutboxEvent { EventType = "session.started", IdempotencyKey = "k1", Payload = "{}" });
        await fixture.Outbox.AddAsync(new OutboxEvent { EventType = "session.ended", IdempotencyKey = "k2", Payload = "{}" });

        var result = await fixture.OutboxHandler().HandleAsync();

        Assert.Equal(2, result.Pushed);
        Assert.Equal(2, fixture.Cloud.Pushed.Count);
        Assert.Equal(0, await fixture.Outbox.CountByStatusAsync(OutboxStatus.Pending));

        var checkpoint = await fixture.Checkpoints.GetByNameAsync(ProcessOutboxHandler.CheckpointName);
        Assert.Equal(2, checkpoint!.Cursor);
    }

    [Fact]
    public async Task Outbox_OnFailure_ReschedulesWithBackoff()
    {
        await using var fixture = Fixture.Create();
        fixture.Cloud.PushSucceeds = false;
        await fixture.Outbox.AddAsync(new OutboxEvent { EventType = "e", IdempotencyKey = "k1", Payload = "{}" });

        var result = await fixture.OutboxHandler().HandleAsync();

        Assert.Equal(0, result.Pushed);
        Assert.True(result.CloudUnavailable);

        var stored = await fixture.Context.OutboxEvents.FirstAsync();
        Assert.Equal(OutboxStatus.Pending, stored.Status);
        Assert.Equal(1, stored.Attempts);
        Assert.NotNull(stored.NextAttemptAt);
        Assert.NotNull(stored.LastError);
    }

    [Fact]
    public async Task Outbox_WhenCloudNotConfigured_DoesNothing()
    {
        await using var fixture = Fixture.Create();
        fixture.Cloud.IsConfigured = false;
        await fixture.Outbox.AddAsync(new OutboxEvent { EventType = "e", IdempotencyKey = "k1", Payload = "{}" });

        var result = await fixture.OutboxHandler().HandleAsync();

        Assert.True(result.CloudUnavailable);
        Assert.Equal(1, await fixture.Outbox.CountByStatusAsync(OutboxStatus.Pending));
    }

    [Fact]
    public async Task Inbox_AppliesCustomerUpsert_AndAdvancesCheckpoint()
    {
        await using var fixture = Fixture.Create();
        fixture.Cloud.Changes.Add(new CloudChange(1, "ext-1", "customer.upsert", "{\"name\":\"Ali\",\"phoneNumber\":\"09120000000\"}"));
        fixture.Cloud.NextCursor = 1;

        var result = await fixture.InboxHandler().HandleAsync();

        Assert.Equal(1, result.Applied);
        Assert.Equal(1, await fixture.Context.Customers.CountAsync());

        var checkpoint = await fixture.Checkpoints.GetByNameAsync(ProcessInboxHandler.CheckpointName);
        Assert.Equal(1, checkpoint!.Cursor);
    }

    [Fact]
    public async Task Inbox_IsIdempotent_ForDuplicateExternalId()
    {
        await using var fixture = Fixture.Create();
        fixture.Cloud.Changes.Add(new CloudChange(1, "ext-1", "customer.upsert", "{\"name\":\"Ali\",\"phoneNumber\":\"09120000000\"}"));
        fixture.Cloud.NextCursor = 1;
        await fixture.InboxHandler().HandleAsync();

        fixture.Cloud.Changes.Clear();
        fixture.Cloud.Changes.Add(new CloudChange(1, "ext-1", "customer.upsert", "{\"name\":\"Ali\",\"phoneNumber\":\"09120000000\"}"));
        var second = await fixture.InboxHandler().HandleAsync();

        Assert.Equal(1, second.Skipped);
        Assert.Equal(0, second.Applied);
        Assert.Equal(1, await fixture.Context.Customers.CountAsync());
        Assert.Equal(1, await fixture.Context.InboxMessages.CountAsync());
    }

    [Fact]
    public async Task Inbox_UnknownMessageType_IsMarkedFailed()
    {
        await using var fixture = Fixture.Create();
        fixture.Cloud.Changes.Add(new CloudChange(1, "ext-x", "unknown.type", "{}"));
        fixture.Cloud.NextCursor = 1;

        var result = await fixture.InboxHandler().HandleAsync();

        Assert.Equal(1, result.Failed);
        Assert.Equal(1, await fixture.Inbox.CountByStatusAsync(InboxStatus.Failed));
    }

    [Fact]
    public async Task Inbox_CustomerUpsert_UpdatesExistingCustomer()
    {
        await using var fixture = Fixture.Create();
        await fixture.Customers.CreateAsync(new Customer { Name = "Old", PhoneNumber = "09120000000" });

        fixture.Cloud.Changes.Add(new CloudChange(1, "ext-1", "customer.upsert", "{\"name\":\"New\",\"phoneNumber\":\"09120000000\"}"));
        fixture.Cloud.NextCursor = 1;

        await fixture.InboxHandler().HandleAsync();

        Assert.Equal(1, await fixture.Context.Customers.CountAsync());
        var customer = await fixture.Context.Customers.FirstAsync();
        Assert.Equal("New", customer.Name);
    }
}
