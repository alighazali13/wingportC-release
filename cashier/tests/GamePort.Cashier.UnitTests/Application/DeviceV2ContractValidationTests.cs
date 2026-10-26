using FluentValidation.TestHelper;
using GamePort.Cashier.Application.Validators;
using GamePort.Cashier.Contracts.Requests;
using Xunit;

namespace GamePort.Cashier.UnitTests.Application;

/// <summary>
/// C2-A validation tests for Device V2 contracts.
/// </summary>
public class DeviceV2ContractValidationTests
{
    // ===== RegisterDeviceRequest =====

    [Fact]
    public void Register_PC_RequiresIp()
    {
        var v = new RegisterDeviceRequestValidator();
        var req = new RegisterDeviceRequest { Name = "PC-01", Type = "GamingPC" };
        var result = v.Validate(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterDeviceRequest.IpAddress));
    }

    [Fact]
    public void Register_PC_WithIp_Succeeds()
    {
        var v = new RegisterDeviceRequestValidator();
        var req = new RegisterDeviceRequest { Name = "PC-01", Type = "GamingPC", IpAddress = "192.168.1.101" };
        Assert.True(v.Validate(req).IsValid);
    }

    [Fact]
    public void Register_PS5_WithoutIp_Succeeds()
    {
        var v = new RegisterDeviceRequestValidator();
        var req = new RegisterDeviceRequest { Name = "PS5-01", Type = "PlayStation" };
        Assert.True(v.Validate(req).IsValid);
    }

    [Fact]
    public void Register_PS5_WithIp_Succeeds()
    {
        var v = new RegisterDeviceRequestValidator();
        var req = new RegisterDeviceRequest { Name = "PS5-01", Type = "PlayStation", IpAddress = "192.168.1.102" };
        Assert.True(v.Validate(req).IsValid);
    }

    [Fact]
    public void Register_InvalidIp_Rejected()
    {
        var v = new RegisterDeviceRequestValidator();
        var req = new RegisterDeviceRequest { Name = "PC-01", Type = "GamingPC", IpAddress = "not-an-ip" };
        var result = v.Validate(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterDeviceRequest.IpAddress));
    }

    [Fact]
    public void Register_TooLongIp_Rejected()
    {
        var v = new RegisterDeviceRequestValidator();
        var req = new RegisterDeviceRequest
        {
            Name = "PC-01",
            Type = "GamingPC",
            IpAddress = new string('1', 46)
        };
        var result = v.Validate(req);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Register_InvalidType_Rejected()
    {
        var v = new RegisterDeviceRequestValidator();
        var req = new RegisterDeviceRequest { Name = "X", Type = "InvalidType", IpAddress = "1.2.3.4" };
        var result = v.Validate(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("نوع دستگاه"));
    }

    [Fact]
    public void Register_PS5_ConsoleModel_MaxLength20()
    {
        var v = new RegisterDeviceRequestValidator();
        var ok = new RegisterDeviceRequest { Name = "PS5", Type = "PlayStation", ConsoleModel = "Slim" };
        Assert.True(v.Validate(ok).IsValid);

        var tooLong = new RegisterDeviceRequest { Name = "PS5", Type = "PlayStation", ConsoleModel = new string('X', 21) };
        Assert.False(v.Validate(tooLong).IsValid);
    }

    [Fact]
    public void Register_PS5_ControllerCount_Range()
    {
        var v = new RegisterDeviceRequestValidator();
        Assert.True(v.Validate(new RegisterDeviceRequest { Name = "PS5", Type = "PlayStation", ControllerCount = 2 }).IsValid);
        Assert.False(v.Validate(new RegisterDeviceRequest { Name = "PS5", Type = "PlayStation", ControllerCount = 0 }).IsValid);
        Assert.False(v.Validate(new RegisterDeviceRequest { Name = "PS5", Type = "PlayStation", ControllerCount = 5 }).IsValid);
    }

    [Fact]
    public void Register_PS5_MaxPlayers_Range()
    {
        var v = new RegisterDeviceRequestValidator();
        Assert.True(v.Validate(new RegisterDeviceRequest { Name = "PS5", Type = "PlayStation", MaxPlayers = 1 }).IsValid);
        Assert.False(v.Validate(new RegisterDeviceRequest { Name = "PS5", Type = "PlayStation", MaxPlayers = 0 }).IsValid);
        Assert.False(v.Validate(new RegisterDeviceRequest { Name = "PS5", Type = "PlayStation", MaxPlayers = 9 }).IsValid);
    }

    [Fact]
    public void Register_DisplayOrder_NonNegative()
    {
        var v = new RegisterDeviceRequestValidator();
        Assert.True(v.Validate(new RegisterDeviceRequest { Name = "X", Type = "GamingPC", IpAddress = "1.2.3.4", DisplayOrder = 0 }).IsValid);
        Assert.False(v.Validate(new RegisterDeviceRequest { Name = "X", Type = "GamingPC", IpAddress = "1.2.3.4", DisplayOrder = -1 }).IsValid);
    }

    // ===== UpdateDeviceRequest =====

    [Fact]
    public void Update_RequiresNameAndXmin()
    {
        var v = new UpdateDeviceRequestValidator();
        var req = new UpdateDeviceRequest { Name = "", Xmin = "" };
        var result = v.Validate(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateDeviceRequest.Name));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateDeviceRequest.Xmin));
    }

    [Fact]
    public void Update_ValidInput_Succeeds()
    {
        var v = new UpdateDeviceRequestValidator();
        var req = new UpdateDeviceRequest
        {
            Name = "PC-01 Renamed",
            IpAddress = "192.168.1.105",
            ZoneId = Guid.NewGuid(),
            PricingGroupId = Guid.NewGuid(),
            DisplayOrder = 1,
            Xmin = "123"
        };
        Assert.True(v.Validate(req).IsValid);
    }

    [Fact]
    public void Update_MaintenanceNotes_MaxLength500()
    {
        var v = new UpdateDeviceRequestValidator();
        var ok = new UpdateDeviceRequest { Name = "X", Xmin = "1", MaintenanceNotes = new string('A', 500) };
        Assert.True(v.Validate(ok).IsValid);
        var tooLong = new UpdateDeviceRequest { Name = "X", Xmin = "1", MaintenanceNotes = new string('A', 501) };
        Assert.False(v.Validate(tooLong).IsValid);
    }

    [Fact]
    public void Update_InvalidXminToken_Rejected()
    {
        var v = new UpdateDeviceRequestValidator();
        var result = v.Validate(new UpdateDeviceRequest { Name = "X", Xmin = "not-a-number" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateDeviceRequest.Xmin));
    }

    [Fact]
    public void Update_InvalidIp_Rejected()
    {
        var v = new UpdateDeviceRequestValidator();
        var result = v.Validate(new UpdateDeviceRequest { Name = "X", IpAddress = "not-an-ip", Xmin = "1" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateDeviceRequest.IpAddress));
    }

    [Fact]
    public void Update_ValidIpv6_Succeeds()
    {
        var v = new UpdateDeviceRequestValidator();
        var req = new UpdateDeviceRequest { Name = "X", IpAddress = "fe80::1", Xmin = "1" };
        Assert.True(v.Validate(req).IsValid);
    }

    [Fact]
    public void Register_ValidIpv6_Succeeds()
    {
        var v = new RegisterDeviceRequestValidator();
        var req = new RegisterDeviceRequest { Name = "PC", Type = "GamingPC", IpAddress = "fe80::1" };
        Assert.True(v.Validate(req).IsValid);
    }

    // ===== ActivationRequest =====

    [Fact]
    public void Activation_RequiresXmin()
    {
        var v = new ActivationRequestValidator();
        Assert.False(v.Validate(new ActivationRequest { IsActive = true, Xmin = "" }).IsValid);
        Assert.True(v.Validate(new ActivationRequest { IsActive = true, Xmin = "42" }).IsValid);
    }

    // ===== OperationalStatusRequest =====

    [Fact]
    public void Status_Available_Succeeds()
    {
        var v = new OperationalStatusRequestValidator();
        Assert.True(v.Validate(new OperationalStatusRequest { OperationalStatus = "Available", Xmin = "1" }).IsValid);
    }

    [Fact]
    public void Status_Maintenance_Succeeds()
    {
        var v = new OperationalStatusRequestValidator();
        Assert.True(v.Validate(new OperationalStatusRequest { OperationalStatus = "Maintenance", Xmin = "1" }).IsValid);
    }

    [Fact]
    public void Status_InUse_Rejected()
    {
        var v = new OperationalStatusRequestValidator();
        var result = v.Validate(new OperationalStatusRequest { OperationalStatus = "InUse", Xmin = "1" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("InUse"));
    }

    [Fact]
    public void Status_RequiresXmin()
    {
        var v = new OperationalStatusRequestValidator();
        var result = v.Validate(new OperationalStatusRequest { OperationalStatus = "Available", Xmin = "" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(OperationalStatusRequest.Xmin));
    }

    // ===== ZoneRequest =====

    [Fact]
    public void Zone_RequiresName()
    {
        var v = new ZoneRequestValidator();
        Assert.False(v.Validate(new ZoneRequest { Name = "" }).IsValid);
        Assert.True(v.Validate(new ZoneRequest { Name = "Main Hall" }).IsValid);
    }

    [Fact]
    public void Zone_DisplayOrder_NonNegative()
    {
        var v = new ZoneRequestValidator();
        Assert.True(v.Validate(new ZoneRequest { Name = "Z", DisplayOrder = 0 }).IsValid);
        Assert.False(v.Validate(new ZoneRequest { Name = "Z", DisplayOrder = -1 }).IsValid);
    }

    // ===== PricingGroupRequest =====

    [Fact]
    public void PricingGroup_RequiresName()
    {
        var v = new PricingGroupRequestValidator();
        Assert.False(v.Validate(new PricingGroupRequest { Name = "" }).IsValid);
    }

    [Fact]
    public void PricingGroup_NullDeviceType_Succeeds()
    {
        var v = new PricingGroupRequestValidator();
        Assert.True(v.Validate(new PricingGroupRequest { Name = "PC Standard", DeviceType = null }).IsValid);
        Assert.True(v.Validate(new PricingGroupRequest { Name = "PC Standard", DeviceType = "" }).IsValid);
    }

    [Fact]
    public void PricingGroup_ValidDeviceType_Succeeds()
    {
        var v = new PricingGroupRequestValidator();
        Assert.True(v.Validate(new PricingGroupRequest { Name = "G1", DeviceType = "GamingPC" }).IsValid);
        Assert.True(v.Validate(new PricingGroupRequest { Name = "G1", DeviceType = "PlayStation" }).IsValid);
    }

    [Fact]
    public void PricingGroup_InvalidDeviceType_Rejected()
    {
        var v = new PricingGroupRequestValidator();
        var result = v.Validate(new PricingGroupRequest { Name = "G1", DeviceType = "Xbox" });
        Assert.False(result.IsValid);
    }
}
