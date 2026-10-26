using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using Xunit;

namespace GamePort.Cashier.UnitTests.Application;

/// <summary>
/// C2-A tests for PricingGroup.DeviceType schema and contract compatibility.
/// </summary>
public class PricingGroupSchemaTests
{
    [Fact]
    public void NewPricingGroup_DeviceTypeIsNull()
    {
        var group = new PricingGroup { Name = "Standard" };
        Assert.Null(group.DeviceType);
    }

    [Fact]
    public void PricingGroup_CanSetDeviceType()
    {
        var group = new PricingGroup { Name = "PC Only", DeviceType = DeviceType.GamingPC };
        Assert.Equal(DeviceType.GamingPC, group.DeviceType);
    }

    [Fact]
    public void PricingGroup_CanSetPlayStation()
    {
        var group = new PricingGroup { Name = "PS5 Only", DeviceType = DeviceType.PlayStation };
        Assert.Equal(DeviceType.PlayStation, group.DeviceType);
    }

    [Fact]
    public void PricingGroup_NullDeviceType_MeansAnyType()
    {
        // NULL = compatible with any device type (per C2-A plan)
        var group = new PricingGroup { Name = "Universal", DeviceType = null };
        Assert.Null(group.DeviceType);
    }

    [Fact]
    public void PricingGroup_DefaultIsActive()
    {
        var group = new PricingGroup { Name = "X" };
        Assert.True(group.IsActive);
    }

    [Fact]
    public void PricingGroup_DescriptionIsNullByDefault()
    {
        var group = new PricingGroup { Name = "X" };
        Assert.Null(group.Description);
    }
}

/// <summary>
/// C2-A tests for V1 contract backward compatibility.
/// </summary>
public class DeviceV1CompatibilityTests
{
    [Fact]
    public void DeviceResponse_StillExposesV1Fields()
    {
        var device = new Device
        {
            Id = Guid.Parse("dedae8d1-7121-406a-afa5-81b7e76a9e07"),
            Name = "PC-01",
            Type = DeviceType.GamingPC,
            Status = DeviceStatus.Available,
            IpAddress = "192.168.1.101",
            MacAddress = null,
            ClientIdentity = "dev-abc123",
            IsConnected = false,
            LastHeartbeat = null,
            CurrentGameId = null
        };

        // V1 fields must remain on the entity
        Assert.Equal("PC-01", device.Name);
        Assert.Equal(DeviceType.GamingPC, device.Type);
        Assert.Equal(DeviceStatus.Available, device.Status);
        Assert.Equal("192.168.1.101", device.IpAddress);
        Assert.False(device.IsConnected);
    }

    [Fact]
    public void Device_V2Defaults_AreCorrect()
    {
        var device = new Device { Name = "X", Type = DeviceType.GamingPC, IpAddress = "1.2.3.4" };
        Assert.True(device.IsActive);
        Assert.Equal(OperationalStatus.Available, device.OperationalStatus);
        Assert.Equal(ConnectivityStatus.NotApplicable, device.ConnectivityStatus);
        Assert.Equal(ConnectionMode.Client, device.ConnectionMode);
        Assert.Equal(0, device.DisplayOrder);
        Assert.Null(device.ZoneId);
        Assert.Null(device.PricingGroupId);
    }

    [Fact]
    public void Device_IPAddress_IsNullable()
    {
        var device = new Device { Name = "PS5", Type = DeviceType.PlayStation };
        Assert.Null(device.IpAddress);
    }

    [Fact]
    public void Device_xmin_IsUint()
    {
        var device = new Device();
        Assert.Equal(0u, device.xmin);
    }
}
