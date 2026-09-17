using GamePort.Cashier.Application.Validators;
using GamePort.Cashier.Contracts.Requests;
using Xunit;

namespace GamePort.Cashier.UnitTests.Application;

public class RequestValidatorTests
{
    [Fact]
    public void LoginRequest_RequiresUsernameAndPassword()
    {
        var validator = new LoginRequestValidator();

        var invalid = validator.Validate(new LoginRequest { Username = "", Password = "" });
        var valid = validator.Validate(new LoginRequest { Username = "admin", Password = "secret" });

        Assert.False(invalid.IsValid);
        Assert.True(valid.IsValid);
    }

    [Fact]
    public void RechargeWalletRequest_RejectsNonPositiveAmountAndBadMethod()
    {
        var validator = new RechargeWalletRequestValidator();

        var invalid = validator.Validate(new RechargeWalletRequest
        {
            CustomerId = Guid.NewGuid(),
            Amount = 0,
            Method = "Bitcoin"
        });
        var valid = validator.Validate(new RechargeWalletRequest
        {
            CustomerId = Guid.NewGuid(),
            Amount = 10_000,
            Method = "Cash"
        });

        Assert.False(invalid.IsValid);
        Assert.True(valid.IsValid);
    }

    [Fact]
    public void CreateGameRequest_RejectsUnknownDeviceType()
    {
        var validator = new CreateGameRequestValidator();

        var invalid = validator.Validate(new CreateGameRequest { Name = "Game", SupportedDeviceType = "Xbox" });
        var valid = validator.Validate(new CreateGameRequest { Name = "Game", SupportedDeviceType = "PlayStation" });

        Assert.False(invalid.IsValid);
        Assert.True(valid.IsValid);
    }

    [Fact]
    public void CreateReservationRequest_RequiresEndAfterStart()
    {
        var validator = new CreateReservationRequestValidator();
        var start = DateTime.UtcNow;

        var invalid = validator.Validate(new CreateReservationRequest
        {
            CustomerId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            StartTime = start,
            EndTime = start.AddMinutes(-10)
        });

        Assert.False(invalid.IsValid);
    }
}
