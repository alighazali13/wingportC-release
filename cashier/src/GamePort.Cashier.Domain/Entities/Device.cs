using GamePort.Cashier.Domain.Common;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Domain.Entities;

public class Device : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public DeviceType Type { get; set; }

    // Legacy status field — kept for backward compatibility during transition.
    public DeviceStatus Status { get; set; }

    // --- V2 fields ---
    public Guid? ZoneId { get; set; }
    public Guid? PricingGroupId { get; set; }
    public bool IsActive { get; set; } = true;
    public OperationalStatus OperationalStatus { get; set; } = OperationalStatus.Available;
    public ConnectivityStatus ConnectivityStatus { get; set; } = ConnectivityStatus.NotApplicable;
    public ConnectionMode ConnectionMode { get; set; } = ConnectionMode.Client;
    public int DisplayOrder { get; set; }
    public string? MaintenanceNotes { get; set; }
    public DateTime? LastMaintenanceAt { get; set; }

    // PS5-specific (nullable, only meaningful when Type == PlayStation)
    public string? ConsoleModel { get; set; }
    public int? ControllerCount { get; set; }
    public int? MaxPlayers { get; set; }

    // Network — IpAddress now nullable (required for PC at API layer, optional for PS5)
    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }

    // Client identity / security (PC Client)
    public string? HardwareInfo { get; set; }
    public string? ClientIdentity { get; set; }
    public string? ClientSecretHash { get; set; }
    public DateTime? CredentialIssuedAt { get; set; }

    // Connectivity tracking (legacy fields kept for backward compatibility)
    public bool IsConnected { get; set; }
    public DateTime? LastHeartbeat { get; set; }
    public Guid? CurrentGameId { get; set; }

    // Navigation
    public Zone? Zone { get; set; }
    public PricingGroup? PricingGroup { get; set; }

    /// <summary>
    /// PostgreSQL xmin system column for optimistic concurrency.
    /// Mapped via IsRowVersion() in CashierDbContext. Not a physical application column.
    /// </summary>
    public uint xmin { get; set; }
}
