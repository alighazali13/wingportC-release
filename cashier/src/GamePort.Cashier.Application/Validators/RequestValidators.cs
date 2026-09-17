using FluentValidation;
using GamePort.Cashier.Contracts.Requests;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6).MaximumLength(200);
    }
}

public class RegisterDeviceRequestValidator : AbstractValidator<RegisterDeviceRequest>
{
    public RegisterDeviceRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(t => Enum.TryParse<DeviceType>(t, true, out _))
            .WithMessage("نوع دستگاه نامعتبر است.");
        RuleFor(x => x.IpAddress).NotEmpty().MaximumLength(45);
    }
}

public class RegisterCustomerRequestValidator : AbstractValidator<RegisterCustomerRequest>
{
    public RegisterCustomerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PhoneNumber).MaximumLength(20);
    }
}

public class StartSessionRequestValidator : AbstractValidator<StartSessionRequest>
{
    public StartSessionRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.DeviceId).NotEmpty();
    }
}

public class RechargeWalletRequestValidator : AbstractValidator<RechargeWalletRequest>
{
    public RechargeWalletRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Method)
            .NotEmpty()
            .Must(m => Enum.TryParse<PaymentMethod>(m, true, out _))
            .WithMessage("روش پرداخت نامعتبر است.");
    }
}

public class CreateReservationRequestValidator : AbstractValidator<CreateReservationRequest>
{
    public CreateReservationRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.DeviceId).NotEmpty();
        RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime)
            .WithMessage("زمان پایان رزرو باید بعد از زمان شروع باشد.");
    }
}

public class CreateGameRequestValidator : AbstractValidator<CreateGameRequest>
{
    public CreateGameRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SupportedDeviceType)
            .NotEmpty()
            .Must(t => Enum.TryParse<DeviceType>(t, true, out _))
            .WithMessage("نوع دستگاه نامعتبر است.");
    }
}

public class CreateEmployeeRequestValidator : AbstractValidator<CreateEmployeeRequest>
{
    public CreateEmployeeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Username).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6).MaximumLength(200);
        RuleFor(x => x.Role).NotEmpty();
    }
}

public class CreatePricingRuleRequestValidator : AbstractValidator<CreatePricingRuleRequest>
{
    public CreatePricingRuleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PricePerHour).GreaterThan(0);
        RuleFor(x => x.DeviceType)
            .NotEmpty()
            .Must(t => Enum.TryParse<DeviceType>(t, true, out _))
            .WithMessage("نوع دستگاه نامعتبر است.");
    }
}
