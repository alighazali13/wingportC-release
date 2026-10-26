using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Infrastructure.Hosting;
using Xunit;

namespace GamePort.Cashier.IntegrationTests.Services;

/// <summary>
/// Tests that ContractMapper remains backward-compatible after Device V2 changes.
/// </summary>
public class ContractMapperDeviceV2Tests
{
    [Fact]
    public void ToResponse_NullIpAddress_ReturnsEmptyString()
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = "PS5-01",
            Type = DeviceType.PlayStation,
            Status = DeviceStatus.Available,
            IpAddress = null,
            MacAddress = null
        };

        var response = device.ToResponse();

        Assert.Equal(string.Empty, response.IpAddress);
        Assert.Equal("PS5-01", response.Name);
        Assert.Equal("PlayStation", response.Type);
        Assert.Equal("Available", response.Status);
        Assert.Null(response.MacAddress);
    }

    [Fact]
    public void ToResponse_NullIpAndMac_DoesNotThrow()
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Type = DeviceType.PlayStation,
            Status = DeviceStatus.Available,
            IpAddress = null,
            MacAddress = null
        };

        var ex = Record.Exception(() => device.ToResponse());
        Assert.Null(ex);
    }

    [Fact]
    public void ToRegisteredResponse_NullIpAddress_ReturnsEmptyString()
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = "PS5-01",
            Type = DeviceType.PlayStation,
            Status = DeviceStatus.Available,
            IpAddress = null
        };

        var response = device.ToRegisteredResponse("secret-123");

        Assert.Equal(string.Empty, response.IpAddress);
        Assert.Equal("secret-123", response.ClientSecret);
    }

    [Fact]
    public void ToResponse_ExistingPC01StyleDevice_MapsAllLegacyFields()
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

        var response = device.ToResponse();

        Assert.Equal(Guid.Parse("dedae8d1-7121-406a-afa5-81b7e76a9e07"), response.Id);
        Assert.Equal("PC-01", response.Name);
        Assert.Equal("GamingPC", response.Type);
        Assert.Equal("Available", response.Status);
        Assert.Equal("192.168.1.101", response.IpAddress);
        Assert.Equal("dev-abc123", response.ClientIdentity);
        Assert.False(response.IsConnected);
        Assert.Null(response.LastHeartbeat);
        Assert.Null(response.CurrentGameId);
    }

    [Fact]
    public void Device_XminProperty_ExistsAndIsUint()
    {
        var device = new Device();
        // xmin defaults to 0 for new entities; EF populates it on load from PostgreSQL
        Assert.Equal(0u, device.xmin);
    }
}

