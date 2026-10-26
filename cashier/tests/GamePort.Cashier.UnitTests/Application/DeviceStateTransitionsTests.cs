using FluentAssertions;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using Xunit;

namespace GamePort.Cashier.UnitTests.Application;

/// <summary>
/// C2-C pure-logic tests for the shared device operational-state transition helper.
/// </summary>
public class DeviceStateTransitionsTests
{
    private static Device NewPc(
        DeviceStatus status = DeviceStatus.Available,
        OperationalStatus operational = OperationalStatus.Available,
        ConnectivityStatus connectivity = ConnectivityStatus.Disconnected,
        bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Name = "PC-01",
        Type = DeviceType.GamingPC,
        Status = status,
        OperationalStatus = operational,
        ConnectivityStatus = connectivity,
        IsActive = isActive,
        IsConnected = connectivity == ConnectivityStatus.Connected
    };

    private static Device NewPs5(
        DeviceStatus status = DeviceStatus.Available,
        OperationalStatus operational = OperationalStatus.Available) => new()
    {
        Id = Guid.NewGuid(),
        Name = "PS5-01",
        Type = DeviceType.PlayStation,
        Status = status,
        OperationalStatus = operational,
        ConnectivityStatus = ConnectivityStatus.NotApplicable,
        IsActive = true
    };

    // ===== Session start / end =====

    [Fact]
    public void MarkSessionStart_SetsInUse_OnBothStatusFields()
    {
        var device = NewPc();
        DeviceStateTransitions.MarkSessionStart(device);
        device.OperationalStatus.Should().Be(OperationalStatus.InUse);
        device.Status.Should().Be(DeviceStatus.InUse);
    }

    [Fact]
    public void MarkSessionEnd_InUseToAvailable_OnBothStatusFields()
    {
        var device = NewPc(DeviceStatus.InUse, OperationalStatus.InUse);
        DeviceStateTransitions.MarkSessionEnd(device);
        device.OperationalStatus.Should().Be(OperationalStatus.Available);
        device.Status.Should().Be(DeviceStatus.Available);
    }

    [Fact]
    public void MarkSessionEnd_PreservesMaintenance()
    {
        var device = NewPc(DeviceStatus.Maintenance, OperationalStatus.Maintenance);
        DeviceStateTransitions.MarkSessionEnd(device);
        device.OperationalStatus.Should().Be(OperationalStatus.Maintenance);
        device.Status.Should().Be(DeviceStatus.Maintenance);
    }

    [Fact]
    public void MarkSessionEnd_DoesNotChangeConnectivityOrIsActive()
    {
        var device = NewPc(DeviceStatus.InUse, OperationalStatus.InUse, ConnectivityStatus.Disconnected, isActive: false);
        DeviceStateTransitions.MarkSessionEnd(device);
        device.ConnectivityStatus.Should().Be(ConnectivityStatus.Disconnected);
        device.IsActive.Should().BeFalse();
    }

    [Fact]
    public void MarkSessionEnd_NonInUseDevice_IsNoOpOnOperational()
    {
        var device = NewPc(DeviceStatus.Maintenance, OperationalStatus.Maintenance);
        DeviceStateTransitions.MarkSessionEnd(device);
        device.OperationalStatus.Should().Be(OperationalStatus.Maintenance);
    }

    // ===== SignalR connect =====

    [Fact]
    public void MarkClientConnected_SetsConnected_NeverChangesOperational()
    {
        var device = NewPc(DeviceStatus.Disconnected, OperationalStatus.Available, ConnectivityStatus.Disconnected);
        device.IsConnected = false;
        DeviceStateTransitions.MarkClientConnected(device);
        device.ConnectivityStatus.Should().Be(ConnectivityStatus.Connected);
        device.OperationalStatus.Should().Be(OperationalStatus.Available);
        device.Status.Should().Be(DeviceStatus.Available);
    }

    [Fact]
    public void MarkClientConnected_Reconnect_DoesNotClearMaintenance()
    {
        var device = NewPc(DeviceStatus.Maintenance, OperationalStatus.Maintenance, ConnectivityStatus.Disconnected);
        DeviceStateTransitions.MarkClientConnected(device);
        device.OperationalStatus.Should().Be(OperationalStatus.Maintenance);
        device.Status.Should().Be(DeviceStatus.Maintenance);
        device.ConnectivityStatus.Should().Be(ConnectivityStatus.Connected);
    }

    [Fact]
    public void MarkClientConnected_DoesNotMarkInUseDeviceAvailable()
    {
        var device = NewPc(DeviceStatus.InUse, OperationalStatus.InUse, ConnectivityStatus.Disconnected);
        DeviceStateTransitions.MarkClientConnected(device);
        device.OperationalStatus.Should().Be(OperationalStatus.InUse);
        device.Status.Should().Be(DeviceStatus.InUse);
    }

    [Fact]
    public void MarkClientConnected_PlayStation_StaysNotApplicable()
    {
        var device = NewPs5();
        DeviceStateTransitions.MarkClientConnected(device);
        device.ConnectivityStatus.Should().Be(ConnectivityStatus.NotApplicable);
    }

    // ===== SignalR disconnect / heartbeat timeout =====

    [Fact]
    public void MarkClientDisconnected_AvailableDevice_LegacyBecomesDisconnected()
    {
        var device = NewPc(DeviceStatus.Available, OperationalStatus.Available, ConnectivityStatus.Connected);
        device.IsConnected = true;
        DeviceStateTransitions.MarkClientDisconnected(device);
        device.ConnectivityStatus.Should().Be(ConnectivityStatus.Disconnected);
        device.Status.Should().Be(DeviceStatus.Disconnected);
        device.OperationalStatus.Should().Be(OperationalStatus.Available);
    }

    [Fact]
    public void MarkClientDisconnected_InUseDevice_KeepsInUse()
    {
        var device = NewPc(DeviceStatus.InUse, OperationalStatus.InUse, ConnectivityStatus.Connected);
        DeviceStateTransitions.MarkClientDisconnected(device);
        device.OperationalStatus.Should().Be(OperationalStatus.InUse);
        device.Status.Should().Be(DeviceStatus.InUse);
        device.ConnectivityStatus.Should().Be(ConnectivityStatus.Disconnected);
    }

    [Fact]
    public void MarkClientDisconnected_MaintenanceDevice_KeepsMaintenance()
    {
        var device = NewPc(DeviceStatus.Maintenance, OperationalStatus.Maintenance, ConnectivityStatus.Connected);
        DeviceStateTransitions.MarkClientDisconnected(device);
        device.OperationalStatus.Should().Be(OperationalStatus.Maintenance);
        device.Status.Should().Be(DeviceStatus.Maintenance);
    }

    [Fact]
    public void MarkClientDisconnected_PlayStation_StaysNotApplicable()
    {
        var device = NewPs5();
        DeviceStateTransitions.MarkClientDisconnected(device);
        device.ConnectivityStatus.Should().Be(ConnectivityStatus.NotApplicable);
    }

    // ===== Maintenance transitions =====

    [Fact]
    public void MarkMaintenanceEntered_SetsMaintenanceOnBothFields()
    {
        var device = NewPc();
        DeviceStateTransitions.MarkMaintenanceEntered(device);
        device.OperationalStatus.Should().Be(OperationalStatus.Maintenance);
        device.Status.Should().Be(DeviceStatus.Maintenance);
    }

    [Fact]
    public void MarkMaintenanceExited_ConnectedDevice_BecomesAvailable()
    {
        var device = NewPc(DeviceStatus.Maintenance, OperationalStatus.Maintenance, ConnectivityStatus.Connected);
        DeviceStateTransitions.MarkMaintenanceExited(device);
        device.OperationalStatus.Should().Be(OperationalStatus.Available);
        device.Status.Should().Be(DeviceStatus.Available);
    }

    [Fact]
    public void MarkMaintenanceExited_DisconnectedDevice_LegacyBecomesDisconnected()
    {
        var device = NewPc(DeviceStatus.Maintenance, OperationalStatus.Maintenance, ConnectivityStatus.Disconnected);
        DeviceStateTransitions.MarkMaintenanceExited(device);
        device.OperationalStatus.Should().Be(OperationalStatus.Available);
        device.Status.Should().Be(DeviceStatus.Disconnected);
    }

    [Fact]
    public void MarkMaintenanceExited_PlayStation_BecomesAvailable()
    {
        var device = NewPs5(DeviceStatus.Maintenance, OperationalStatus.Maintenance);
        DeviceStateTransitions.MarkMaintenanceExited(device);
        device.OperationalStatus.Should().Be(OperationalStatus.Available);
        device.Status.Should().Be(DeviceStatus.Available);
    }

    // ===== Reconnect-after-timeout round trip =====

    [Fact]
    public void DisconnectThenReconnect_RoundTrip_RestoresAvailable()
    {
        var device = NewPc();
        DeviceStateTransitions.MarkClientConnected(device);
        DeviceStateTransitions.MarkClientDisconnected(device);
        device.Status.Should().Be(DeviceStatus.Disconnected);
        DeviceStateTransitions.MarkClientConnected(device);
        device.Status.Should().Be(DeviceStatus.Available);
        device.OperationalStatus.Should().Be(OperationalStatus.Available);
        device.ConnectivityStatus.Should().Be(ConnectivityStatus.Connected);
    }
}
