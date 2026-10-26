using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.Common;

/// <summary>
/// Single source of truth for Device operational/connectivity/legacy state transitions.
/// Pure static logic — no I/O — so every writer (session lifecycle, SignalR hub,
/// heartbeat monitor, recovery service) applies identical, audited rules.
///
/// Rules:
/// - OperationalStatus is authoritative for Available/InUse/Maintenance.
/// - ConnectivityStatus is authoritative for Connected/Disconnected/NotApplicable.
/// - IsActive is independent and never touched here.
/// - Legacy DeviceStatus stays compatible:
///     Maintenance  -> Maintenance (never cleared by connectivity or session end)
///     InUse        -> InUse      (never cleared by connectivity)
///     Available    -> Disconnected only when a connected GamingPC loses its connection
///     Disconnected -> Available only when the GamingPC reconnects
/// - SignalR/heartbeat NEVER change OperationalStatus.
/// - Session completion NEVER reactivates an inactive device and NEVER changes connectivity.
/// </summary>
public static class DeviceStateTransitions
{
    /// <summary>Session start: Available → InUse (operational + legacy). Caller must have validated eligibility.</summary>
    public static void MarkSessionStart(Device device)
    {
        device.OperationalStatus = OperationalStatus.InUse;
        device.Status = DeviceStatus.InUse;
    }

    /// <summary>
    /// Session end (completed/cancelled/expired/interrupted/transferred-away):
    /// InUse → Available. Maintenance is preserved. Connectivity and IsActive untouched.
    /// </summary>
    public static void MarkSessionEnd(Device device)
    {
        if (device.OperationalStatus == OperationalStatus.InUse)
        {
            device.OperationalStatus = OperationalStatus.Available;
        }

        if (device.Status == DeviceStatus.InUse)
        {
            device.Status = DeviceStatus.Available;
        }
    }

    /// <summary>
    /// SignalR client connected / heartbeat received on a live connection.
    /// Sets connectivity for GamingPC only. Legacy Status: Disconnected/Offline → Available.
    /// InUse and Maintenance are never overwritten. OperationalStatus untouched.
    /// </summary>
    public static void MarkClientConnected(Device device)
    {
        if (device.Type == DeviceType.PlayStation)
        {
            // Manual devices: connectivity is not applicable and must not be required.
            device.ConnectivityStatus = ConnectivityStatus.NotApplicable;
            return;
        }

        device.ConnectivityStatus = ConnectivityStatus.Connected;

        if (device.Status is DeviceStatus.Disconnected or DeviceStatus.Offline)
        {
            device.Status = DeviceStatus.Available;
        }
    }

    /// <summary>
    /// SignalR client disconnected or heartbeat timeout on a GamingPC.
    /// Legacy Status: Available → Disconnected only. InUse and Maintenance are preserved
    /// so a reconnect or session end can never observe a lost operational state.
    /// OperationalStatus untouched.
    /// </summary>
    public static void MarkClientDisconnected(Device device)
    {
        if (device.Type == DeviceType.PlayStation)
        {
            device.ConnectivityStatus = ConnectivityStatus.NotApplicable;
            return;
        }

        device.ConnectivityStatus = ConnectivityStatus.Disconnected;

        if (device.Status == DeviceStatus.Available)
        {
            device.Status = DeviceStatus.Disconnected;
        }
    }

    /// <summary>Entering Maintenance (manual, no open session). Legacy Status becomes Maintenance.</summary>
    public static void MarkMaintenanceEntered(Device device)
    {
        device.OperationalStatus = OperationalStatus.Maintenance;
        device.Status = DeviceStatus.Maintenance;
    }

    /// <summary>
    /// Leaving Maintenance → Available. Legacy Status: Disconnected when a GamingPC is
    /// currently disconnected, otherwise Available.
    /// </summary>
    public static void MarkMaintenanceExited(Device device)
    {
        device.OperationalStatus = OperationalStatus.Available;
        device.Status = device.ConnectivityStatus == ConnectivityStatus.Disconnected
            ? DeviceStatus.Disconnected
            : DeviceStatus.Available;
    }
}
