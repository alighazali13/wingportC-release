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

        // IP: required for GamingPC, optional for PlayStation
        RuleFor(x => x.IpAddress)
            .NotEmpty()
            .When(x => IsGamingPc(x.Type))
            .WithMessage("آدرس IP برای رایانه گیمینگ الزامی است.");

        RuleFor(x => x.IpAddress)
            .MaximumLength(45)
            .When(x => !string.IsNullOrWhiteSpace(x.IpAddress));

        RuleFor(x => x.IpAddress)
            .Must(BeValidIp)
            .When(x => !string.IsNullOrWhiteSpace(x.IpAddress))
            .WithMessage("آدرس IP نامعتبر است.");

        RuleFor(x => x.MacAddress).MaximumLength(32);
        RuleFor(x => x.HardwareInfo).MaximumLength(4000);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);

        // PS5-specific fields
        RuleFor(x => x.ConsoleModel).MaximumLength(20)
            .When(x => !string.IsNullOrWhiteSpace(x.ConsoleModel));

        RuleFor(x => x.ControllerCount)
            .InclusiveBetween(1, 4)
            .When(x => x.ControllerCount.HasValue);

        RuleFor(x => x.MaxPlayers)
            .InclusiveBetween(1, 8)
            .When(x => x.MaxPlayers.HasValue);
    }

    private static bool IsGamingPc(string type)
        => Enum.TryParse<DeviceType>(type, true, out var t) && t == DeviceType.GamingPC;

    private static bool BeValidIp(string? value)
    {
        if (!System.Net.IPAddress.TryParse(value!.Trim(), out var address))
        {
            return false;
        }

        return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
               || address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
    }
}

public class UpdateDeviceRequestValidator : AbstractValidator<UpdateDeviceRequest>
{
    public UpdateDeviceRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.IpAddress).MaximumLength(45)
            .When(x => !string.IsNullOrWhiteSpace(x.IpAddress));
        RuleFor(x => x.IpAddress)
            .Must(BeValidIp)
            .When(x => !string.IsNullOrWhiteSpace(x.IpAddress))
            .WithMessage("آدرس IP نامعتبر است.");
        RuleFor(x => x.MacAddress).MaximumLength(32);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaintenanceNotes).MaximumLength(500);
        RuleFor(x => x.ConsoleModel).MaximumLength(20)
            .When(x => !string.IsNullOrWhiteSpace(x.ConsoleModel));
        RuleFor(x => x.ControllerCount).InclusiveBetween(1, 4)
            .When(x => x.ControllerCount.HasValue);
        RuleFor(x => x.MaxPlayers).InclusiveBetween(1, 8)
            .When(x => x.MaxPlayers.HasValue);
        RuleFor(x => x.Xmin).NotEmpty().WithMessage("Xmin برای ویرایش الزامی است.");
        RuleFor(x => x.Xmin)
            .Must(x => uint.TryParse(x, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Xmin))
            .WithMessage("Xmin نامعتبر است.");
    }

    private static bool BeValidIp(string? value)
    {
        if (!System.Net.IPAddress.TryParse(value!.Trim(), out var address))
        {
            return false;
        }

        return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
               || address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
    }
}

public class ActivationRequestValidator : AbstractValidator<ActivationRequest>
{
    public ActivationRequestValidator()
    {
        RuleFor(x => x.Xmin).NotEmpty().WithMessage("Xmin برای تغییر وضعیت فعالی الزامی است.");
        RuleFor(x => x.Xmin)
            .Must(x => uint.TryParse(x, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Xmin))
            .WithMessage("Xmin نامعتبر است.");
    }
}

public class OperationalStatusRequestValidator : AbstractValidator<OperationalStatusRequest>
{
    public OperationalStatusRequestValidator()
    {
        RuleFor(x => x.OperationalStatus)
            .NotEmpty()
            .Must(s => s is "Available" or "Maintenance")
            .WithMessage("وضعیت عملیاتی فقط می‌تواند Available یا Maintenance باشد. InUse توسط چرخه نشست مدیریت می‌شود.");

        RuleFor(x => x.MaintenanceNotes).MaximumLength(500);
        RuleFor(x => x.Xmin).NotEmpty().WithMessage("Xmin برای تغییر وضعیت الزامی است.");
        RuleFor(x => x.Xmin)
            .Must(x => uint.TryParse(x, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Xmin))
            .WithMessage("Xmin نامعتبر است.");
    }
}

public class ZoneRequestValidator : AbstractValidator<ZoneRequest>
{
    public ZoneRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public class PricingGroupRequestValidator : AbstractValidator<PricingGroupRequest>
{
    public PricingGroupRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.DeviceType)
            .Must(t => string.IsNullOrWhiteSpace(t) ||
                       Enum.TryParse<DeviceType>(t, true, out _))
            .WithMessage("نوع دستگاه نامعتبر است. مقدار مجاز: GamingPC, PlayStation یا خالی.");
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
