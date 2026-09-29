using GamePort.Cashier.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GamePort.Cashier.Infrastructure.Data;

public class CashierDbContext : DbContext
{
    public CashierDbContext(DbContextOptions<CashierDbContext> options) : base(options)
    {
    }

    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<DeviceGame> DeviceGames => Set<DeviceGame>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PricingRule> PricingRules => Set<PricingRule>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<SyncCheckpoint> SyncCheckpoints => Set<SyncCheckpoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.MacAddress).HasMaxLength(32);
            entity.Property(e => e.ClientIdentity).HasMaxLength(64);
            entity.Property(e => e.ClientSecretHash).HasMaxLength(256);
            entity.HasIndex(e => e.ClientIdentity).IsUnique();
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.HasIndex(e => e.PhoneNumber);
            entity.HasOne(e => e.Wallet)
                .WithOne(w => w.Customer)
                .HasForeignKey<Wallet>(w => w.CustomerId);
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId);
            entity.HasOne(e => e.Device).WithMany().HasForeignKey(e => e.DeviceId);
            entity.Property(e => e.PriceAtStart).HasPrecision(18, 2);
            entity.Property(e => e.TotalCharged).HasPrecision(18, 2);
            entity.Property(e => e.OperatorInfo).HasMaxLength(200);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.DeviceId);
        });

        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Balance).HasPrecision(18, 2);
        });

        modelBuilder.Entity<WalletTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Wallet).WithMany(w => w.Transactions).HasForeignKey(e => e.WalletId);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.BalanceAfter).HasPrecision(18, 2);
            entity.Property(e => e.Type).HasMaxLength(50).IsRequired();
            entity.Property(e => e.IdempotencyKey).HasMaxLength(200);
            entity.HasIndex(e => e.IdempotencyKey).IsUnique();
            entity.HasIndex(e => e.WalletId);
        });

        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Version).HasMaxLength(50);
            entity.Property(e => e.ExecutablePath).HasMaxLength(500);
            entity.Property(e => e.IconPath).HasMaxLength(500);
        });

        modelBuilder.Entity<DeviceGame>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Device).WithMany().HasForeignKey(e => e.DeviceId);
            entity.HasOne(e => e.Game).WithMany().HasForeignKey(e => e.GameId);
            entity.Property(e => e.InstalledVersion).HasMaxLength(50);
            entity.HasIndex(e => new { e.DeviceId, e.GameId }).IsUnique();
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId);
            entity.HasOne(e => e.Device).WithMany().HasForeignKey(e => e.DeviceId);
            entity.Property(e => e.Source).HasMaxLength(50);
            entity.HasIndex(e => e.StartTime);
            entity.HasIndex(e => new { e.DeviceId, e.StartTime });
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Username).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(256);
            entity.Property(e => e.Role).HasMaxLength(50);
            entity.HasIndex(e => e.Username).IsUnique();
        });

        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.ClockInAt);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).HasMaxLength(100).IsRequired();
            entity.Property(e => e.EntityType).HasMaxLength(100);
            entity.Property(e => e.ActorName).HasMaxLength(200);
            entity.Property(e => e.Source).HasMaxLength(50);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.IdempotencyKey).HasMaxLength(200).IsRequired();
            entity.Property(e => e.OperatorInfo).HasMaxLength(200);
            entity.HasIndex(e => e.IdempotencyKey).IsUnique();
            entity.HasIndex(e => e.CustomerId);
        });

        modelBuilder.Entity<PricingRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PricePerHour).HasPrecision(18, 2);
            entity.HasIndex(e => new { e.DeviceType, e.IsActive });
        });

        modelBuilder.Entity<OutboxEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.IdempotencyKey).HasMaxLength(200).IsRequired();
            entity.HasIndex(e => e.Sequence).IsUnique();
            entity.HasIndex(e => e.IdempotencyKey).IsUnique();
            entity.HasIndex(e => new { e.Status, e.Sequence });
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ExternalId).HasMaxLength(200).IsRequired();
            entity.Property(e => e.MessageType).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.ExternalId).IsUnique();
        });

        modelBuilder.Entity<SyncCheckpoint>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
        });
    }
}
